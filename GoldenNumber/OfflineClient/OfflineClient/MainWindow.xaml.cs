using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

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
            return Tuple.Create(42.0, 1.5);
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var lines = File.ReadAllLines(@"Data\SecondData.txt");

            var meta = lines[0].Split(Delimiter).Select(int.Parse).ToList();
            var totalRound = meta[0];
            var columnCount = meta[1];

            int nearestCount = 0;
            int farthestCount = 0;
            TimeSpan sumTime = TimeSpan.Zero;
            TimeSpan maxTime = TimeSpan.MinValue;
            var scoreHelper = new Utils.ScoreHelper(lines, Delimiter);

            int?[] scores = new int?[columnCount / 2 + 1]; // 使用 Nullable<int> 作为得分的类型，以辨别重头到尾都没有提交过合法值的情况。

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
                        // submitted 变量不为 null，即确实提交了有效的数字。
                        submitted = null;
                    }
                }

                var scoreResult = scoreHelper.CalculateScore(i, submitted);
                scores = scores.Zip(
                    scoreResult.Scores,
                    (accum, thisRound) => thisRound.HasValue ? (thisRound.Value + (accum ?? 0)) : accum // 第一次得到分数时（包括0分），初始化总分数。
                ).ToArray();
                nearestCount += (scoreResult.HasNearest ? 1 : 0);
                farthestCount += (scoreResult.HasFarthest ? 1 : 0);

                // 不管数字有效与否，总是统计运行时间。
                sumTime += br.ElapsedTime;
                maxTime = maxTime < br.ElapsedTime ? br.ElapsedTime : maxTime;

                int? score = scores.Last();
                int ranking = scores.Select((s, idx) => new { Score = s, Index = idx }) // 绑定得分和序号（从0开始）。我们只用唯一序号来标识参加者，而不用可能重复的得分数。
                    .OrderByDescending(si => si.Score) // 先按得分排序
                    .Select((si, orderingIdx) => new { SI = si, OrderingIdx = orderingIdx }) // 按排序后的顺序再赋予一次序号（从0开始）
                    .First(orderedSi => orderedSi.SI.Index == scores.Length - 1) // 找到序号是最大值的数据块，也即当前玩家的。
                    .OrderingIdx + 1; // 排序后的序号 + 1，就是当前玩家的排名（从1开始）。

                NearestText.Text = $"{nearestCount}/{i + 1}";
                FarthestText.Text = $"{farthestCount}/{i + 1}";

                ScoreText.Text = score?.ToString() ?? "N/A";
                ScoreText.Foreground = score > 0 ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);

                MaxAveScoreText.Text = $"{scores.Max()} & {scores.Average():.##}";
                RankingText.Text = $"{ranking}/{scores.Length}";

                AveTimeText.Text = $"{sumTime.TotalMilliseconds / (i + 1)}ms";
                MaxTimeText.Text = $"{maxTime.TotalMilliseconds}ms";
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
