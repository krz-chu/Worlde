using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ToastNotifications;
using ToastNotifications.Lifetime;
using ToastNotifications.Messages;
using ToastNotifications.Position;
/*

Install-Package ToastNotifications
Install-Package ToastNotifications.Messages

*/
namespace Worlde
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public enum TileState { Unknown, Correct, Present, Absent }

        //model kafelków liter
        public class TileModel : System.ComponentModel.INotifyPropertyChanged
        {
            private string _letter = "";
            public string Letter
            {
                get => _letter;
                set { if (_letter != value) { _letter = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Letter))); } }
            }

            private TileState _state = TileState.Unknown;
            public TileState State
            {
                get => _state;
                set { if (_state != value) { _state = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(State))); } }
            }
            public int Row { get; set; }
            public int Col { get; set; }
            public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        }
        //model klawiszy klawiatury
        public class KeyModel : System.ComponentModel.INotifyPropertyChanged
        {
            public string Letter { get; set; } = "";
            private TileState _state = TileState.Unknown;
            public TileState State
            {
                get => _state;
                set { if (_state != value) { _state = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(State))); } }
            }
            public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        }

        public ObservableCollection<TileModel> Tiles { get; } = new ObservableCollection<TileModel>();
        public ObservableCollection<KeyModel> Keys { get; } = new ObservableCollection<KeyModel>();
        public ObservableCollection<KeyModel> KeysRow1 { get; } = new ObservableCollection<KeyModel>();
        public ObservableCollection<KeyModel> KeysRow2 { get; } = new ObservableCollection<KeyModel>();
        public ObservableCollection<KeyModel> KeysRow3 { get; } = new ObservableCollection<KeyModel>();

        private int currentRow = 0;
        private int currentCol = 0;
        private bool gameOver = false;

        private readonly System.Collections.Generic.List<string> wordsList = new System.Collections.Generic.List<string>();
        private string secretWord = string.Empty;
        public string SecretWord
        {
            get => secretWord;
            private set
            {
                if (secretWord != value)
                {
                    secretWord = value;
                    OnPropertyChanged(nameof(SecretWord));
                }
            }
        }
        
        
        private readonly System.Collections.Generic.HashSet<string> allowedWords;

        private Notifier? _notifier;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        private void LoadWordsFromFile(bool allowFallback = true)
        {
        //słowa z words.txt   
            wordsList.Clear();
            
            var wordsFileName = SecretWordLength switch
            {
                4 => "words4.txt",
                6 => "words6.txt",
                7 => "words7.txt",
                8 => "words8.txt",
                _ => "words.txt",
            };

            string? found = FindFileInParents(wordsFileName);

            if (found != null)
            {
                try
                {
                    var lines = File.ReadAllLines(found);
                    foreach (var ln in lines)
                    {
                    var s = ln.Trim();
                        if (s.Length == SecretWordLength && s.All(char.IsLetter)) wordsList.Add(s.ToUpperInvariant());
                    }
                }
                catch { }
            }

            if (wordsList.Count == 0)
            {
                // jak sie wszystko wysra
                
                Notify("Nie znaleziono pliku words.txt lub jest pusty.", "Uwaga", NotificationKind.Warning);
                // Spróbuj dodać słowa z pliku do_dodania.txt \ jeśli istnieje
                try
                {
                    AddWordsFromDoFile(wordsFileName);
                }
                catch { }

                if (wordsList.Count == 0)
                {
                    // jeśli nadal brak słów spróbuj przywrócić domyślne ustawienia \ 5 liter 6 prób
                    if (allowFallback)
                    {
                        Notify("Nie znaleziono pliku ze słowami dla wybranej długości. Przywrucono domyślne ustawienia (5x6).", "Uwaga", NotificationKind.Warning);
                        _secretWordLength = 5;
                        OnPropertyChanged(nameof(SecretWordLength));
                        MaxAttempts = 6;
                        InitializeTiles();

                        // spróbuj ponownie wczytać dla długości 5, ale już bez dalszego fallbacku
                        LoadWordsFromFile(allowFallback: false);
                        // zaktualizuj allowedWords jeśli istnieje
                        allowedWords?.Clear();
                        if (wordsList != null) foreach (var w in wordsList) allowedWords?.Add(w);
                        // zaktualizuj licznik słów, rozpocznij nową grę
                        OnPropertyChanged(nameof(WordsCount));
                        NewGame();
                        return;
                    }

                    Notify("Nie znaleziono pliku words.txt lub jest pusty.", "Uwaga", NotificationKind.Warning);
                    return;
                }

                if (wordsList.Count == 0 && SecretWordLength == 5)
                {
                    Notify("Nie znaleziono pliku words.txt lub jest pusty. Użyto domyślnej listy słów.", "Uwaga", NotificationKind.Warning);
                    wordsList.AddRange(new[]
                    {
                        "APPLE", "GRAPE", "PLANE", "BRICK", "CANDY", "HELLO", "POWER", "LIVED",
                        "WHICH", "THERE", "THEIR", "ABOUT", "WOULD", "THESE", "OTHER", "WORDS",
                        "COULD", "WRITE", "FIRST", "WATER", "AFTER", "WHERE", "RIGHT", "THINK",
                        "THREE", "YEARS", "PLACE", "SOUND", "POINT", "TIMES", "HEARD", "WHOLE",
                        "WHITE", "GIVEN", "MEANS", "MUSIC", "MILES", "THING", "TODAY", "LATER",
                        "USING", "MONEY", "LINES", "ORDER", "GROUP", "AMONG", "LEARN", "KNOWN",
                        "SPACE", "TABLE", "EARLY", "TREES", "SHORT", "HANDS", "STATE", "BLACK",
                        "SHOWN", "STOOD", "FRONT", "VOICE", "KINDS", "MAKES", "COMES", "CLOSE"
                    });
                }
                
            }
            OnPropertyChanged(nameof(WordsCount));
        }

        public int WordsCount => wordsList.Count;

        private int _secretWordLength = 5;
        public int SecretWordLength
        {
            get => _secretWordLength;
            set
            {
                if (_secretWordLength != value)
                {
                    _secretWordLength = value;
                    OnPropertyChanged(nameof(SecretWordLength));
                    InitializeTiles();
                    LoadWordsFromFile();
                    allowedWords?.Clear();
                    if (wordsList != null) foreach (var w in wordsList) allowedWords?.Add(w);
                    NewGame();
                }
            }
        }

        private int _maxAttempts = 6;
        public int MaxAttempts
        {
            get => _maxAttempts;
            set
            {
                if (_maxAttempts != value)
                {
                    _maxAttempts = value;
                    OnPropertyChanged(nameof(MaxAttempts));
                    InitializeTiles();
                }
            }
        }

        private string? FindFileInParents(string filename, int maxLevels = 6)
        {
            //jeśli plik jest gdzieś indziej do 6 folderów do góry \\ gógr
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < maxLevels; i++)
            {
                var p = Path.Combine(dir, filename);
                if (File.Exists(p)) return p;
                var parent = Path.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(parent) || parent == dir) break;
                dir = parent;
            }
            return null;
        }

        private void AddWords_Click(object sender, RoutedEventArgs e)
        {
            // z do_dodania.txt do odpowiedniego words txt
            var wordsFileName = SecretWordLength switch
            {
                4 => "words4.txt",
                6 => "words6.txt",
                7 => "words7.txt",
                8 => "words8.txt",
                _ => "words.txt",
            };
            try
            {
                int added = AddWordsFromDoFile(wordsFileName);
                OnPropertyChanged(nameof(WordsCount));
                Notify($"Dodano {added} słów", "Informacja", NotificationKind.Success);
            }
            catch (Exception ex)
            {
                Notify($"Błąd przy dodawaniu słów: {ex.Message}", "Błąd", NotificationKind.Error);
            }
        }
        private int AddWordsFromDoFile(string wordsFileName)
        {
            // helper który szuka i dodaje
            var addPath = FindFileInParents("do_dodania.txt") ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "do_dodania.txt");
            if (!File.Exists(addPath))
            {
                Notify("Brak pliku do_dodania.txt", "Błąd", NotificationKind.Warning);
                return 0;
            }

            var wordsPath = FindFileInParents(wordsFileName) ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, wordsFileName);
            if (!File.Exists(wordsPath))
            {
                File.WriteAllText(wordsPath, string.Empty);
            }

            var lines = File.ReadAllLines(addPath);
            if (lines.Length == 0)
            {
                Notify("Plik do_dodania.txt jest pusty", "Informacja", NotificationKind.Information);
                return 0;
            }

            var current = (allowedWords != null)
                ? new HashSet<string>(allowedWords, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(wordsList, StringComparer.OrdinalIgnoreCase);

            var toAppend = new System.Collections.Generic.List<string>();
            int added = 0;
            foreach (var ln in lines)
            {
                var s = (ln ?? "").Trim();
                if (s.Length != SecretWordLength) continue;
                if (!s.All(char.IsLetter)) continue;
                var up = s.ToUpperInvariant();
                if (current.Contains(up)) continue;
                toAppend.Add(up.ToLowerInvariant());
                wordsList.Add(up);
                if (allowedWords != null) allowedWords.Add(up);
                current.Add(up);
                added++;
            }

            if (toAppend.Count > 0)
            {
                File.AppendAllLines(wordsPath, toAppend);
            }
            return added;
        }
        /// <summary>
        /// start --------------------------------------------------------
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            DataContext = this;
            // toast notification
            _notifier = new Notifier(cfg =>
            {
                
                cfg.PositionProvider = new WindowPositionProvider(
                    parentWindow: this,
                    corner: Corner.TopLeft,
                    offsetX: 10,
                    offsetY: 10);

                cfg.LifetimeSupervisor = new TimeAndCountBasedLifetimeSupervisor(
                    notificationLifetime: TimeSpan.FromSeconds(3),
                    maximumNotificationCount: MaximumNotificationCount.FromCount(5));

                cfg.Dispatcher = Application.Current.Dispatcher;
            });

            // słówka z words.txt
            LoadWordsFromFile();
            allowedWords = new System.Collections.Generic.HashSet<string>(wordsList, StringComparer.OrdinalIgnoreCase);
            InitializeTiles();
            // kafelki klawiatury 
            var row1 = "QWERTYUIOP".Select(ch => ch.ToString());
            var row2 = "ASDFGHJKL".Select(ch => ch.ToString());
            var row3 = new[] { "ENTER" }.Concat("ZXCVBNM".Select(ch => ch.ToString())).Concat(new[] { "DELETE" });
            foreach (var l in row1) KeysRow1.Add(new KeyModel { Letter = l, State = TileState.Unknown });
            foreach (var l in row2) KeysRow2.Add(new KeyModel { Letter = l, State = TileState.Unknown });
            foreach (var l in row3) KeysRow3.Add(new KeyModel { Letter = l, State = TileState.Unknown });

            // nowe super sekretne słowo
            var rnd = new Random();
            if (wordsList.Count == 0) LoadWordsFromFile();
            SecretWord = wordsList[rnd.Next(wordsList.Count)].ToUpperInvariant();
        }

        public void InitializeTiles()
        {
            // kafelki MaxAttempts X SecretWordLength
            Tiles.Clear();
            for (int r = 0; r < MaxAttempts; r++)
            {
                for (int c = 0; c < SecretWordLength; c++)
                {
                    Tiles.Add(new TileModel { Row = r, Col = c, Letter = "", State = TileState.Unknown });
                }
            }
        }

        private void KeyButton_Click(object? sender, RoutedEventArgs e)
        {
            //obsłóga klawiatury
            if (gameOver) return;
            if (sender is Button button && button.Content != null)
            {
                var txt = button.Content.ToString() ?? "";
                if (txt == "ENTER") { OnEnterPressed(); return; }
                if (txt == "DELETE") { OnDeletePressed(); return; }
                OnLetterPressed(txt);
            }
        }

        private void OnLetterPressed(string letter)
        {
            //obsłóga liter
            if (currentCol >= SecretWordLength) return;
            var idx = currentRow * SecretWordLength + currentCol;
            Tiles[idx].Letter = letter.ToUpperInvariant();
            Tiles[idx].State = TileState.Unknown;
            currentCol++;
        }

        private void OnDeletePressed()
        {
            //delete
            if (currentCol <= 0) return;
            currentCol--;
            var idx = currentRow * SecretWordLength + currentCol;
            Tiles[idx].Letter = "";
            Tiles[idx].State = TileState.Unknown;
        }

        private void OnEnterPressed()
        {
            //main magic
            if (currentCol < SecretWordLength) { Notify("Za mało liter", "Uwaga", NotificationKind.Warning); return; }
            var guess = string.Concat(Tiles.Skip(currentRow * SecretWordLength).Take(SecretWordLength).Select(t => t.Letter));
            var guessUp = guess.ToUpperInvariant();

            // walidacja z words.txt
            if (!allowedWords.Contains(guessUp))
            {
                Notify("To nie jest słowo", "Błąd", NotificationKind.Error);
                return;
            }

            var secret = SecretWord;

            if (guessUp == secret)
            {
                for (int i = 0; i < SecretWordLength; i++)
                {
                    var tile = Tiles[currentRow * SecretWordLength + i];
                    tile.State = TileState.Correct;
                    UpdateKeyState(tile.Letter, TileState.Correct);
                }
                Notify("Wygrałeś!", "Sukces", NotificationKind.Success);
                gameOver = true;
                return;
            }

            // oznaczanie: dobre, jest, niema, niewiadomo
            for (int i = 0; i < SecretWordLength; i++)
            {
                var tile = Tiles[currentRow * SecretWordLength + i];
                var ch = tile.Letter;
                if (string.IsNullOrEmpty(ch))
                {
                    tile.State = TileState.Absent;
                    UpdateKeyState(ch, TileState.Absent);
                    continue;
                }

                var ch0 = ch[0];
                if (secret[i] == ch0)
                {
                    tile.State = TileState.Correct;
                    UpdateKeyState(ch, TileState.Correct);
                }
                else if (secret.Contains(ch))
                {
                    tile.State = TileState.Present;
                    UpdateKeyState(ch, TileState.Present);
                }
                else
                {
                    tile.State = TileState.Absent;
                    UpdateKeyState(ch, TileState.Absent);
                }
            }

            //dalej
            currentRow++;
            currentCol = 0;
            if (currentRow >= MaxAttempts)
            {
                //game over = true
                gameOver = true;
                Notify($"Koniec gry. Szukane słowo: {SecretWord}", "Informacja", NotificationKind.Information);
            }
        }

        private enum NotificationKind { Information, Success, Warning, Error }

        private void Notify(string message, string caption = "Informacja", NotificationKind kind = NotificationKind.Information)
        {
            //powiadomienia push na telefonie
            //urzytkowniku zgadzasz się?
            // tak \\ przypomnij mi póżniej
            try
            {
                if (_notifier != null)
                {
                    switch (kind)
                    {
                        case NotificationKind.Success: _notifier.ShowSuccess(message); break;
                        case NotificationKind.Warning: _notifier.ShowWarning(message); break;
                        case NotificationKind.Error: _notifier.ShowError(message); break;
                        default: _notifier.ShowInformation(message); break;
                    }
                    return;
                }
            }
            catch { }

            try
            {
                //jak toastnotification nie działa to MessageBox
                var icon = MessageBoxImage.None;
                if (kind == NotificationKind.Error) icon = MessageBoxImage.Error;
                else if (kind == NotificationKind.Warning) icon = MessageBoxImage.Warning;
                else if (kind == NotificationKind.Success) icon = MessageBoxImage.Information;
                else icon = MessageBoxImage.Information;
                MessageBox.Show(message, caption, MessageBoxButton.OK, icon);
            }
            catch { }
        }

        private void UpdateKeyState(string letter, TileState state)
        {
            var key = KeysRow1.Concat(KeysRow2).Concat(KeysRow3).FirstOrDefault(k => k.Letter == letter);
            if (key == null) return;
            // zachowaj: dobre, jest, niema, niewiadomo
            if (state == TileState.Correct) key.State = TileState.Correct;
            else if (state == TileState.Present && key.State != TileState.Correct) key.State = TileState.Present;
            else if (state == TileState.Absent && key.State == TileState.Unknown) key.State = TileState.Absent;
        }
        private void NewGame()
        {
            // reset
            foreach (var t in Tiles)
            {
                t.Letter = "";
                t.State = TileState.Unknown;
            }
            foreach (var k in KeysRow1.Concat(KeysRow2).Concat(KeysRow3)) k.State = TileState.Unknown;
            currentRow = 0; currentCol = 0; gameOver = false;

            if (wordsList.Count == 0) LoadWordsFromFile();

            // jeśli nadal pusta — przerwij i powiadom
            if (wordsList.Count == 0)
            {
                Notify("Brak słów do gry. Sprawdź plik ze słowami.", "Błąd", NotificationKind.Error);
                return;
            }

            var rnd = new Random();
            SecretWord = wordsList[rnd.Next(wordsList.Count)].ToUpperInvariant();
            Notify("Nowa gra", "Informacja", NotificationKind.Information);
        }

        private void NewGame_Click(object sender, RoutedEventArgs e) => NewGame();

        private void RadioButton_Click(object? sender, RoutedEventArgs e)
        {
            //długość słowa
            if (sender is RadioButton rb && rb.IsChecked == true)
            {
                var val = rb.CommandParameter?.ToString() ?? rb.Content?.ToString();
                if (int.TryParse(val, out int length)) SecretWordLength = length;
                else SecretWordLength = 5;
                InitializeTiles();
                NewGame();
                LoadWordsFromFile();
            }
        }

        private void RadioButton_Click_1(object sender, RoutedEventArgs e)
        {
            //ilość prób
            if (sender is RadioButton rb && rb.IsChecked == true)
            {
                var val = rb.CommandParameter?.ToString() ?? rb.Content?.ToString();
                if (int.TryParse(val, out int attempts)) MaxAttempts = attempts;
                else MaxAttempts = 6;
                NewGame();
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            //from keyboard on key pressededed
            if (e.Key == Key.Enter)
            {
                OnEnterPressed();
                e.Handled = true;
            } 
            else if (e.Key == Key.Back)
            {
                OnDeletePressed();
                e.Handled = true;
            }
            else
            {
                var keyChar = e.Key.ToString();
                //MessageBox.Show($"Key pressed: {keyChar}"); //debug
                if (!string.IsNullOrEmpty(keyChar) && keyChar.Length == 1 && char.IsLetter(keyChar[0]))
                {
                    OnLetterPressed(keyChar.ToUpperInvariant());
                    e.Handled = true;
                }
            }
        }
    }
}