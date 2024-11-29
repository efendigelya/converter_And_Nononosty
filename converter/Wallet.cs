using System.Collections.Generic;

namespace converter
{
    public class Wallet
    {
        private readonly Dictionary<string, decimal> _balances = new();

        public decimal GetBalance(string currency)
        {
            return _balances.TryGetValue(currency, out var balance) ? balance : 0;
        }

        public void AddAmount(string currency, decimal amount)
        {
            if (_balances.ContainsKey(currency))
            {
                _balances[currency] += amount;
            }
            else
            {
                _balances[currency] = amount;
            }
        }

        public decimal GetEquivalent(string targetCurrency, Dictionary<string, CurrencyRate> rates)
        {
            decimal total = 0;
            foreach (var currency in _balances.Keys)
            {
                if (rates.TryGetValue(currency, out var rate))
                {
                    total += _balances[currency] * rate.Value / rate.Nominal;
                }
            }
            return total / rates[targetCurrency].Value * rates[targetCurrency].Nominal;  
        }

        public IEnumerable<string> GetAllCurrencies()
        {
            return _balances.Keys.ToList();
        }
    }

}
