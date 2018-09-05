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

        private const char Delimiter = '\t';
        private const string NewLine = "\n";

        // !!! 需要提供实现的函数。
        // 请注意，该函数会在后台线程执行。
        private static Tuple<double, double> GetNumber(string input)
        {
            System.Threading.Thread.Sleep(40);
            return Tuple.Create(42.0, 1.5d);
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var lines = File.ReadAllLines(@"Data\SecondData.txt");

            var meta = lines[0].Split(Delimiter).Select(int.Parse).ToList();
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
                    var input = $"{i}{Delimiter}{columnCount}";
                    if (i > 0)
                    {
                        // 避免没有数据时，末尾会有一个空行。否则可能影响解析。
                        input += NewLine;
                        input += string.Join(NewLine, lines.Skip(1).Take(i));
                    }

                    var botResult = new BotResult();
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        var submittedNumbers = GetNumber(input);
                        botResult.Candidate = submittedNumbers;
                    }
                    catch (Exception)
                    {
                    }

                    botResult.ElapsedTime = sw.Elapsed;
                    return botResult;
                });

                // 检查提交的数字是否有效。
                var submitted = br.Candidate == null ? null : YieldFromTuple(br.Candidate).ToArray();
                if (submitted != null)
                {
                    if (!submitted.All(IsValidNumber))
                    {
                        submitted = null;
                    }
                }

                // submitted 变量不为 null，即确实提交了有效的数字。
                if (submitted != null)
                {
                    // 最后一回合时，还需要取最后一行数据来计算结果，所以最后一行不能用作历史数据。
                    var numbersThisRound = lines[i + 1].Split(Delimiter)
                        .Skip(1) // 在计算当前轮的结果时，去掉历史数据里的黄金点，并重新计算。
                        .Select(double.Parse).ToList();
                    numbersThisRound.AddRange(submitted);
                    var newG = numbersThisRound.Average() * 0.618;

                    // 每个数据都有序号（从0开始），以便我们追踪。当前的参与者提交的数据，放在最后。
                    int[] indexes = { numbersThisRound.Count - 1, numbersThisRound.Count - 2 };

                    // 不只是进行简单地排序。
                    // 由于可能有一样的数据，所以我们先按数据进行分组，然后再排序。
                    var sortedGroup = numbersThisRound
                        .Select((n, index) => new { Diff = Abs(n - newG), Index = index }) // 一种 Enumerable.Select 方法的重载，是同时提供了从0开始的序号的。
                        .GroupBy(diffAndIndex => diffAndIndex.Diff)
                        .OrderBy(group => group.Key) // group.Key 就是重复的那个数字。
                        .ToList();

                    // 由于我们对可能相同的数据做了分组，又记了序号，所以我们只需检查当前提交的数对应的序号，
                    // 是否属于距离最小组，或距离最大组即可。
                    var nearestIndexes = new HashSet<int>(sortedGroup.First().Select(di => di.Index));
                    var farthestIndexes = new HashSet<int>(sortedGroup.Last().Select(di => di.Index));

                    int thisRoundScore = 0;

                    var submittedNotNearestIndexes = indexes.Where(idx => !nearestIndexes.Contains(idx)).ToArray();
                    if (submittedNotNearestIndexes.Length < indexes.Length)
                    {
                        // 存在某个提交的数，属于距黄金点最近的一组。并且最多只得一次分。
                        thisRoundScore += numbersThisRound.Count / 2 - 1; // 统计人数时不计当前玩家。
                        nearestCount++;
                    }

                    if (submittedNotNearestIndexes.Any(farthestIndexes.Contains))
                    {
                        // 除了最近的数以外，还存在最远的数。并且最多只扣一次分。
                        // 如果所有数都一样，从上面可知，算是得分。
                        thisRoundScore -= 2;
                        farthestCount++;
                    }

                    score += thisRoundScore;
                }

                sumTime += br.ElapsedTime;
                maxTime = maxTime < br.ElapsedTime ? br.ElapsedTime : maxTime;

                NearestText.Text = $"{nearestCount}/{i + 1}";
                FarthestText.Text = $"{farthestCount}/{i + 1}";
                ScoreText.Text = $"{score}";
                AveTimeText.Text = $"{sumTime.TotalMilliseconds / (i + 1)}ms";
                MaxTimeText.Text = $"{maxTime.TotalMilliseconds}ms";

                ScoreText.Foreground = score < 0 ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.Green);
            }
        }

        private static bool IsValidNumber(double num)
        {
            return 0 < num && num < 100;
        }

        private static IEnumerable<double> YieldFromTuple(Tuple<double, double> t)
        {
            yield return t.Item1;
            yield return t.Item2;
        }
    }

    public class BotResult
    {
        public Tuple<double, double> Candidate { get; set; }

        public TimeSpan ElapsedTime { get; set; }
    }
}
