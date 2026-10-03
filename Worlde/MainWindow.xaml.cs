using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using System.ComponentModel;
using ToastNotifications;
using ToastNotifications.Lifetime;
using ToastNotifications.Position;
using ToastNotifications.Messages;

//Install - Package ToastNotifications
//Install - Package ToastNotifications.Messages


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
        private void LoadWordsFromFile()
        {
        //słowa z words.txt   
            wordsList.Clear();
            
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            string? found = null;
            for (int i = 0; i < 6; i++)
            {
                var p = Path.Combine(dir, "words.txt");
                if (File.Exists(p)) { found = p; break; }
                var parent = Path.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(parent) || parent == dir) break;
                dir = parent;
            }

            if (found != null)
            {
                try
                {
                    var lines = File.ReadAllLines(found);
                    foreach (var ln in lines)
                    {
                        var s = ln.Trim();
                        if (s.Length == 5 && s.All(char.IsLetter)) wordsList.Add(s.ToUpperInvariant());
                    }
                }
                catch { }
            }

            if (wordsList.Count == 0)
            {
                // jak sie wszystko wysra to jest to coś
                wordsList.AddRange(new[] { "APPLE", "GRAPE", "PLANE", "BRICK", "CANDY", "HELLO" });
            }
            OnPropertyChanged(nameof(WordsCount));
        }

        public int WordsCount => wordsList.Count;

        private string? FindFileInParents(string filename, int maxLevels = 6)
        {
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
            //dodaj do listy słów
            try
            {
                var addPath = FindFileInParents("do_dodania.txt") ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "do_dodania.txt");
                if (!File.Exists(addPath))
                {
                    Notify("Brak pliku do_dodania.txt", "Błąd", NotificationKind.Warning);
                    return;
                }

                var wordsPath = FindFileInParents("words.txt") ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "words.txt");
                if (!File.Exists(wordsPath))
                {
                    File.WriteAllText(wordsPath, string.Empty);
                }

                var lines = File.ReadAllLines(addPath);
                if (lines.Length == 0)
                {
                    Notify("Plik do_dodania.txt jest pusty", "Informacja", NotificationKind.Information);
                    return;
                }

                var current = new HashSet<string>(allowedWords, StringComparer.OrdinalIgnoreCase);
                var toAppend = new System.Collections.Generic.List<string>();
                int added = 0;
                foreach (var ln in lines)
                {
                    // czy dobre słowo 
                    var s = (ln ?? "").Trim();
                    if (s.Length != 5) continue;
                    if (!s.All(char.IsLetter)) continue;
                    var up = s.ToUpperInvariant();
                    if (current.Contains(up)) continue;
                    toAppend.Add(up.ToLowerInvariant());
                    wordsList.Add(up);
                    allowedWords.Add(up);
                    current.Add(up);
                    added++;
                }

                if (toAppend.Count > 0)
                {
                    // dodaj do words.txt
                    File.AppendAllLines(wordsPath, toAppend);
                    
                }

                // clear do_dodania.txt
                File.WriteAllText(addPath, string.Empty);

                // ile słów jest teraz
                OnPropertyChanged(nameof(WordsCount));

                Notify($"Dodano {added} słów", "Informacja", NotificationKind.Success);
            }
            catch (Exception ex)
            {
                Notify($"Błąd przy dodawaniu słów: {ex.Message}", "Błąd", NotificationKind.Error);
            }
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
                    parentWindow: Application.Current.MainWindow,
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

            // kafelki 6x5 do liter
            for (int r = 0; r < 6; r++)
            {
                for (int c = 0; c < 5; c++)
                {
                    Tiles.Add(new TileModel { Letter = "", State = TileState.Unknown, Row = r, Col = c });
                }
            }

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
            //obsłóga klawiatury v2
            if (currentCol >= 5) return;
            var idx = currentRow * 5 + currentCol;
            Tiles[idx].Letter = letter.ToUpperInvariant();
            Tiles[idx].State = TileState.Unknown;
            currentCol++;
        }

        private void OnDeletePressed()
        {
            //delete
            if (currentCol <= 0) return;
            currentCol--;
            var idx = currentRow * 5 + currentCol;
            Tiles[idx].Letter = "";
            Tiles[idx].State = TileState.Unknown;
        }

        private void OnEnterPressed()
        {
            //main magic
            if (currentCol < 5) { Notify("Za mało liter", "Uwaga", NotificationKind.Warning); return; }
            var guess = string.Concat(Tiles.Skip(currentRow * 5).Take(5).Select(t => t.Letter));
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
                for (int i = 0; i < 5; i++)
                {
                    var tile = Tiles[currentRow * 5 + i];
                    tile.State = TileState.Correct;
                    UpdateKeyState(tile.Letter, TileState.Correct);
                }
                Notify("Wygrałeś!", "Sukces", NotificationKind.Success);
                gameOver = true;
                return;
            }

            // oznaczanie: dobre, jest, niema, niewiadomo
            for (int i = 0; i < 5; i++)
            {
                var tile = Tiles[currentRow * 5 + i];
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
            if (currentRow >= 6)
            {
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
            catch
            {
                
            }

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
            //reset
            foreach (var t in Tiles)
            {
                t.Letter = "";
                t.State = TileState.Unknown;
            }
            foreach (var k in KeysRow1.Concat(KeysRow2).Concat(KeysRow3)) k.State = TileState.Unknown;
            currentRow = 0; currentCol = 0; gameOver = false;
            var rnd = new Random();
            if (wordsList.Count == 0) LoadWordsFromFile();
            SecretWord = wordsList[rnd.Next(wordsList.Count)].ToUpperInvariant();
            Notify("Nowa gra", "Informacja", NotificationKind.Information);
        }

        private void NewGame_Click(object sender, RoutedEventArgs e) => NewGame();
        
    }
}