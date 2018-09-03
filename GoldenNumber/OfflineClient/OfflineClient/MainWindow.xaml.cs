using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Math;

namespace OfflineClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var lines = File.ReadAllLines(@"Data\SecondData.txt");

            var meta = lines[0].Split('\t').Select(int.Parse).ToList();
            var totalRound = meta[0];
            var columnCount = meta[1];

            int score = 0;
            int nearestCount = 0;
            int farthestCount = 0;
            TimeSpan sumTime = TimeSpan.Zero;
            TimeSpan maxTime = TimeSpan.MinValue;

            for (int i = 0; i < totalRound; i++)
            {
                var br = await Task.Run(() =>
                {
                    var input = $"{i}\t{columnCount}\n" + string.Join("\n", lines.Skip(1).Take(i));
                    var sw = Stopwatch.StartNew();

                    var candidate = GetNumber(input);

                    var elapsed = sw.Elapsed;

                    return new BotResult
                    {
                        Candidate = candidate,
                        ElapsedTime = elapsed,
                    };
                });

                sumTime += br.ElapsedTime;
                maxTime = TimeSpan.FromTicks(Max(maxTime.Ticks, br.ElapsedTime.Ticks));

                var submitted = br.Candidate;
                var numbersThisRound = lines[i + 1].Split('\t')
                    .Skip(1) // exclude old G
                    .Select(double.Parse).ToList();
                numbersThisRound.Add(submitted.Item1);
                numbersThisRound.Add(submitted.Item2);

                int participantIndex1 = numbersThisRound.Count - 1;
                int participantIndex2 = numbersThisRound.Count - 2;

                var newG = numbersThisRound.Average() * 0.618;

                var sortedGroup = numbersThisRound
                    .Select((n, index) => new { Diff = Abs(n - newG), Index = index })
                    .GroupBy(diffAndIndex => diffAndIndex.Diff)
                    .OrderBy(group => group.Key)
                    .ToList();

                var nearestIndexes = new HashSet<int>(sortedGroup.First().Select(di => di.Index));
                var farthestIndexes = new HashSet<int>(sortedGroup.Last().Select(di => di.Index));
                int thisRoundScore = 0;

                if (nearestIndexes.Contains(participantIndex1) || nearestIndexes.Contains(participantIndex2))
                {
                    thisRoundScore += numbersThisRound.Count / 2;
                    nearestCount++;
                }

                if (farthestIndexes.Contains(participantIndex1) || farthestIndexes.Contains(participantIndex2))
                {
                    thisRoundScore -= 2;
                    farthestCount++;
                }

                score += thisRoundScore;

                NearestText.Text = $"{nearestCount}/{i + 1}";
                FarthestText.Text = $"{farthestCount}/{i + 1}";
                ScoreText.Text = $"{score}";
                AveTimeText.Text = $"{sumTime.TotalMilliseconds / (i + 1)}ms";
                MaxTimeText.Text = $"{maxTime.TotalMilliseconds}ms";

                ScoreText.Foreground = score < 0 ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.Green);

            }
        }

        private static Tuple<double, double> GetNumber(string input)
        {
            System.Threading.Thread.Sleep(40);
            return Tuple.Create(42.0, 1.5d);
        }
    }

    public class BotResult
    {
        public Tuple<double, double> Candidate { get; set; }

        public TimeSpan ElapsedTime { get; set; }
    }
}
