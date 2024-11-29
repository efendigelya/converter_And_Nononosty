using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Diagnostics;
using System;
using System.Timers;

namespace converter;

public partial class MainPage : ContentPage, INotifyPropertyChanged
{
    private readonly HttpClient _httpClient = new();
    private DateTime _selectedDate = DateTime.Today;
    private string _rateDateText;
    private Dictionary<string, CurrencyRate> _ratesCache = new();


    public event PropertyChangedEventHandler PropertyChanged;

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (SetProperty(ref _selectedDate, value))
            {
                Debug.WriteLine($"Дата изменена: {value}");
                _ = LoadRatesAsync();  
            }
        }
    }

    public string RateDateText
    {
        get => _rateDateText;
        set => SetProperty(ref _rateDateText, value);
    }

    public ObservableCollection<string> Currencies { get; } = new();

    private string _sourceCurrency;
    public string SourceCurrency
    {
        get => _sourceCurrency;
        set
        {
            if (SetProperty(ref _sourceCurrency, value))
            {
                if (value == "JPY") 
                {
                    SourceAmount = "100"; 
                }
                CalculateTargetAmount();
            }
        }
    }

    private string _targetCurrency;
    public string TargetCurrency
    {
        get => _targetCurrency;
        set
        {
            if (SetProperty(ref _targetCurrency, value))
                CalculateTargetAmount();
        }
    }

    private string _sourceAmount;
    public string SourceAmount
    {
        get => _sourceAmount;
        set
        {
            if (SetProperty(ref _sourceAmount, value))
            {
                if (decimal.TryParse(value, out var amount))
                {
                    _sourceDecimalAmount = amount;
                    CalculateTargetAmount();
                }
                else
                {
                    _sourceDecimalAmount = 0;
                }
            }
        }
    }

    private string _targetAmount;
    public string TargetAmount
    {
        get => _targetAmount;
        set => SetProperty(ref _targetAmount, value);
    }

    private decimal _sourceDecimalAmount;

    public MainPage()
    {
        InitializeComponent();
        BindingContext = this;
        LoadPreferences();
        _ = LoadRatesAsync();
        /////////
        StartPsychologicalRateTimer(); 

    }
    ////////////////
    private System.Timers.Timer _psychologicalRateTimer;
    private int[] _psychologicalDollarRates = { 2, 5,7, 37, 36, 5000, 66, 68, 73, 85, 88, 70, 44, 35, 200, 10000, 1, 0, 0, 105, 110, 999, 50, 123, 789 }; 
    private Random _random = new Random();
    private int _psychologicalDollarRate;
    private bool _isTimerRunning; 

    private void StartPsychologicalRateTimer()
    {
        _psychologicalRateTimer = new System.Timers.Timer(50); 
        _psychologicalRateTimer.Elapsed += UpdatePsychologicalDollarRate;
        _psychologicalRateTimer.AutoReset = true;
        _psychologicalRateTimer.Start();
        _isTimerRunning = true; 
    }

    private void UpdatePsychologicalDollarRate(object sender, ElapsedEventArgs e)
    {
       
        _psychologicalDollarRate = _psychologicalDollarRates[_random.Next(_psychologicalDollarRates.Length)];
        OnPropertyChanged(nameof(PsychologicalDollarRate));
    }

    public int PsychologicalDollarRate => _psychologicalDollarRate;

    public void ToggleTimer()
    {
        if (_isTimerRunning)
        {
            _psychologicalRateTimer.Stop();
        }
        else
        {
            _psychologicalRateTimer.Start();
        }

        _isTimerRunning = !_isTimerRunning; 
    }
    private void OnToggleTimerButtonClicked(object sender, EventArgs e)
    {
        ToggleTimer(); 
    }
    /////////////////////
    private async Task LoadRatesAsync()
    {
        DateTime date = SelectedDate;
        Debug.WriteLine($"Загрузка курсов для даты: {date}");

        while (true)
        {
            var rates = await GetRatesAsync(date);
            if (rates != null)
            {
                _ratesCache = rates;
                RateDateText = $"Курс на {date:dd.MM.yyyy}";

                UpdateCurrencies();      
                CalculateTargetAmount(); 

                SavePreferences();       
                break;
            }
            date = date.AddDays(-1);
            if (date < DateTime.Today.AddYears(-1))
            {
                Debug.WriteLine("Данные за указанную дату недоступны.");
                RateDateText = "Курсы недоступны для выбранной даты";
                break;
            }
        }
    }


    private async Task<Dictionary<string, CurrencyRate>> GetRatesAsync(DateTime date)
    {
        string url = date.Date == DateTime.Today
            ? "https://www.cbr-xml-daily.ru/daily_json.js"
            : $"https://www.cbr-xml-daily.ru/archive/{date:yyyy'%2F'MM'%2F'dd}/daily_json.js"; 

        Debug.WriteLine($"Запрос URL: {url}");

        try
        {
            var response = await _httpClient.GetFromJsonAsync<JsonElement>(url);
            if (response.TryGetProperty("Valute", out var valute))
            {
                var rates = valute.Deserialize<Dictionary<string, CurrencyRate>>() ?? new();

                foreach (var currency in rates.Values)
                {
                    if (currency.CharCode == "JPY" && currency.Nominal != 1)
                    {
                        currency.Value /= currency.Nominal;
                        currency.Nominal = 1;
                    }
                }
                rates["RUB"] = new CurrencyRate
                {
                    CharCode = "RUB",
                    Name = "Российский рубль",
                    Value = 1,
                    Nominal = 1
                };

                return rates;
            }
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"Ошибка загрузки данных: {ex.Message}");
        }

        return null;
    }

    private void UpdateCurrencies()
    {
        var previousSourceCurrency = SourceCurrency; 
        var previousTargetCurrency = TargetCurrency;

        Currencies.Clear();
        foreach (var rate in _ratesCache.Values)
        {
            Currencies.Add(rate.CharCode);
        }

        if (Currencies.Contains(previousSourceCurrency))
            SourceCurrency = previousSourceCurrency;
        if (Currencies.Contains(previousTargetCurrency))
            TargetCurrency = previousTargetCurrency;

        if (string.IsNullOrEmpty(SourceCurrency) || !Currencies.Contains(SourceCurrency))
            SourceCurrency = "USD";
        if (string.IsNullOrEmpty(TargetCurrency) || !Currencies.Contains(TargetCurrency))
            TargetCurrency = "RUB";
    }

    public void CalculateTargetAmount()
    {
        if (string.IsNullOrEmpty(SourceCurrency) || string.IsNullOrEmpty(TargetCurrency) ||
            !_ratesCache.ContainsKey(SourceCurrency) || !_ratesCache.ContainsKey(TargetCurrency))
        {
            return;
        }

        var sourceRate = _ratesCache[SourceCurrency];
        var targetRate = _ratesCache[TargetCurrency];
        decimal result = (_sourceDecimalAmount * sourceRate.Value / sourceRate.Nominal) * targetRate.Nominal / targetRate.Value;

        TargetAmount = result.ToString("F2");
    }

    private void LoadPreferences()
    {
        SelectedDate = Preferences.Get("SelectedDate", DateTime.Today);
        //SourceCurrency = Preferences.Get("SourceCurrency", "USD");
        //TargetCurrency = Preferences.Get("TargetCurrency", "EUR");
        SourceAmount = Preferences.Get("SourceAmount", "1");
    }

    private void SavePreferences()
    {
        Preferences.Set("SelectedDate", SelectedDate);
        Preferences.Set("SourceCurrency", SourceCurrency);
        Preferences.Set("TargetCurrency", TargetCurrency);
        Preferences.Set("SourceAmount", SourceAmount);
    }

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
    {
        if (Equals(storage, value))
            return false;

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }





    //////////////////////////////////////////////////////////

    private decimal _summarizedWalletBalance;
    public decimal SummarizedWalletBalance
    {
        get => _summarizedWalletBalance;
        private set => SetProperty(ref _summarizedWalletBalance, value);
    }

    public void AddToWallet(string currency, string amountStr)
    {
        if (decimal.TryParse(amountStr, out var amount) && amount > 0)
        {
            _wallet.AddAmount(currency, amount);
            AmountAdded += amount;

            UpdateWalletBalance();
        }
    }


    public void UpdateWalletBalance()
    {
        WalletBalance = _wallet.GetEquivalent(TargetCurrency, _ratesCache);
    }

    private ObservableCollection<WalletBalanceInfo> _balances = new();
    public ObservableCollection<WalletBalanceInfo> Balances
    {
        get => _balances;
        private set => SetProperty(ref _balances, value);
    }
    public void ShowWalletBalance()
    {
        Debug.WriteLine($"Баланс в {TargetCurrency}: {WalletBalance:F2}");
    }
    private void OnAddWalletButtonClicked(object sender, EventArgs e)
    {
        var currency = SourceCurrency; 
        var amount = walletAmountEntry.Text;

        AddToWallet(currency, amount);
        //UpdateWalletBalance(); 
    }

    private async void OnDateSelected(object sender, DateChangedEventArgs e)
    {

        await LoadRatesAsync();
        UpdateSummarizedWalletBalance(); 
    }
    public void UpdateSummarizedWalletBalance()
    {

        SummarizedWalletBalance = _wallet.GetEquivalent(TargetCurrency, _ratesCache);
        OnPropertyChanged(nameof(SummarizedWalletBalance));
    }


    private decimal _walletBalance;
    public decimal WalletBalance
    {
        get => _walletBalance;
        private set => SetProperty(ref _walletBalance, value); 
    }


    private Wallet _wallet = new();  

    private decimal _amountAdded;
    public decimal AmountAdded
    {
        get => _amountAdded;
        private set => SetProperty(ref _amountAdded, value);
    }

    private decimal _totalBalance;
    public decimal TotalBalance
    {
        get => _totalBalance;
        private set => SetProperty(ref _totalBalance, value);
    }
    private void OnUpdateBalanceButtonClicked(object sender, EventArgs e)
    {

        AmountAdded = _wallet.GetBalance(TargetCurrency); 
        TotalBalance = _wallet.GetEquivalent(TargetCurrency, _ratesCache); 
        ShowWalletBalance(); 

    }




    private void OnShowWalletBalancesButtonClicked(object sender, EventArgs e)
    {
        UpdateBalances(); 
    }


    private void UpdateBalances()
    {
        Balances.Clear(); 
        foreach (var currency in _wallet.GetAllCurrencies())
        {
            var amount = _wallet.GetBalance(currency);
            Balances.Add(new WalletBalanceInfo
            {
                Currency = currency,
                Amount = amount
            });
        }
    }
    public class WalletBalanceInfo
    {
        public string Currency { get; set; }
        public decimal Amount { get; set; }
    }

    private async void OnUpdateRatesButtonClicked(object sender, EventArgs e)
    {
        await LoadRatesAsync(); 
    }


    private async void OnShowBalancesButtonClicked(object sender, EventArgs e)
    {
        var balances = _wallet.GetAllCurrencies()
                    .Select(currency => new
                    {
                        Currency = currency,
                        Amount = _wallet.GetBalance(currency),
                        Date = SelectedDate.ToString("dd.MM.yyyy")
                    })
                    .ToList();

        string balanceInfo = string.Join(Environment.NewLine, balances.Select(b => $"{b.Currency}: {b.Amount:F2}"));
        decimal totalBalance = _wallet.GetEquivalent(TargetCurrency, _ratesCache);

        await DisplayAlert("Балансовая информация", $"{balanceInfo}\nОбщая сумма в {TargetCurrency}: {totalBalance:F2}", "OK");
    }



    //////////////////////////////////////////
}

public class CurrencyRate
{
    public string CharCode { get; set; }
    public string Name { get; set; }
    public decimal Value { get; set; }
    public decimal Nominal { get; set; }

    public decimal GetRate(decimal amount)
    {
        return amount * Value / Nominal;
    }
}
