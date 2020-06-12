using CollectionManager.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using StreamCompanionTypes.Enums;

namespace StreamCompanionTypes.DataTypes
{
    public class OutputPattern : EventArgs, INotifyPropertyChanged, ICloneable, IOutputPattern
    {
        private static readonly ObservableCollection<string> LiveTokenNames = new ObservableCollection<string>();

        private string _name;
        private string _pattern = "Your pattern text";
        private OsuStatus _saveEvent = OsuStatus.All;

        public event PropertyChangedEventHandler PropertyChanged;

        [Editable(false)]
        [IgnoreDataMember]
        public Tokens Replacements { get; set; }

        private static readonly ReadOnlyObservableCollection<string> ReadOnlyLiveTokenNames = new ReadOnlyObservableCollection<string>(LiveTokenNames);

        [IgnoreDataMember]
        public ReadOnlyObservableCollection<string> MemoryFormatTokens => ReadOnlyLiveTokenNames;

        [DisplayName("Name")]
        [JsonProperty(PropertyName = "Name")]
        public string Name
        {
            get => _name;
            set
            {
                if (value == _name)
                {
                    return;
                }

                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        [JsonProperty(PropertyName = "Pattern")]
        [DisplayName("Pattern")]
        public string Pattern
        {
            get => _pattern;
            set
            {
                if (value == _pattern)
                {
                    return;
                }

                _pattern = value;
                SetMemoryFormat();
                _compiledFormulas = null;

                OnPropertyChanged(nameof(Pattern));
            }
        }

        private void SetMemoryFormat()
        {
            IsMemoryFormat = MemoryFormatTokens.Select(s => s.ToLower()).Any(_pattern.ToLower().Contains);
        }

        [Editable(false)]
        [DisplayName("Event")]
        [IgnoreDataMember]
        public string SaveEventStr
        {
            get
            {
                if (SaveEvent == OsuStatus.Null)
                    return "Never";

                return SaveEvent.ToString();
            }
        }

        [JsonProperty(PropertyName = "ShowInOsu")]
        [Editable(false)]
        [DisplayName("Ingame")]
        public bool ShowInOsu { get; set; }

        [JsonProperty(PropertyName = "XPosition")]
        [Browsable(false)]
        public int XPosition { get; set; } = 200;

        [JsonProperty(PropertyName = "YPosition")]
        [Browsable(false)]
        public int YPosition { get; set; } = 200;

        [JsonProperty(PropertyName = "Color")]
        [Browsable(false)]
        public Color Color { get; set; } = Color.Red;

        [JsonProperty(PropertyName = "FontName")]
        [Browsable(false)]
        public string FontName { get; set; } = "Arial";

        [JsonProperty(PropertyName = "FontSize")]
        [Browsable(false)]
        public int FontSize { get; set; } = 12;

        [JsonProperty(PropertyName = "Aligment")]
        [Browsable(false)]
        public int Aligment { get; set; } = 0;

        [JsonProperty(PropertyName = "SaveEvent")]
        [Browsable(false)]
        public OsuStatus SaveEvent
        {
            get => _saveEvent;
            set
            {
                if (value == _saveEvent)
                {
                    return;
                }

                _saveEvent = value;
                OnPropertyChanged(nameof(SaveEvent));
                OnPropertyChanged(nameof(SaveEventStr));
            }
        }

        [Browsable(false)]
        [IgnoreDataMember]
        [DisplayName("Memory format")]
        public bool IsMemoryFormat { get; private set; }

        public OutputPattern()
        {
            LiveTokenNames.CollectionChanged += (_, __) => SetMemoryFormat();
        }

        public object Clone()
        {
            return MemberwiseClone();
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string GetFormatedPattern(OsuStatus status = OsuStatus.All)
        {
            if (!CanSave(Pattern, Replacements, SaveEvent, status))
                return string.Empty;

            if (_compiledFormulas == null)
                _compiledFormulas = JaceEngine.Instance.CompileFormulas(Pattern);

            return FormatPattern(Pattern, Replacements, _compiledFormulas);
        }

        private Dictionary<string, (string format, Func<IDictionary<string, double>, double> Func)> _compiledFormulas;

        public static bool CanSave(string pattern, Tokens tokens, OsuStatus saveEvent,
            OsuStatus currentStatus = OsuStatus.All)
        {
            if (tokens == null || (saveEvent & currentStatus) == 0 || string.IsNullOrWhiteSpace(pattern))
                return false;

            foreach (var r in tokens)
            {
                if (CultureInfo.InvariantCulture.CompareInfo.IndexOf(pattern, $"!{r.Key}!",
                    CompareOptions.IgnoreCase) >= 0)
                {
                    if (!r.Value.CanSave(currentStatus))
                        return false;
                }
            }

            return true;
        }

        public static string FormatPattern(string pattern, Tokens tokens, OsuStatus saveEvent,
            OsuStatus currentStatus = OsuStatus.All)
        {
            return CanSave(pattern, tokens, saveEvent, currentStatus) 
                ? FormatPattern(pattern, tokens, null) 
                : string.Empty;
        }
        public static string FormatPattern(string pattern, Tokens tokens, 
            Dictionary<string, (string format, Func<IDictionary<string, double>, double> Func)> compiledFormulas)
        {
            if (compiledFormulas != null)
                pattern = JaceEngine.Instance.FormatFormulas(pattern, compiledFormulas, tokens.NumericTokens);

            foreach (var r in tokens)
            {
                string replacement;
                if (r.Value.Value is null)
                {
                    replacement = "";
                }
                else
                {
                    replacement = r.Value.FormatedValue;
                }

                pattern = pattern.Replace($"!{r.Key}!", replacement,
                    StringComparison.InvariantCultureIgnoreCase);
            }

            return pattern;
        }

        internal static void AddLiveToken(string tokenName)
        {
            tokenName = $"!{tokenName}!";
            if (!LiveTokenNames.Contains(tokenName))
            {
                LiveTokenNames.Add(tokenName);
            }
        }
    }
}
