using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BuckshotRouletteHelper
{
    public partial class MainWindow : Window
    {
        private readonly int initialBullets;
        private readonly int initialChambers;
        private int currentBlanks;

        // Variables to store the last state for undo functionality
        private int lastChambers;
        private int lastBullets;
        private int lastBlanks;
        private bool isUndoOperation = false;

        // Path to the log file
        private readonly string logFilePath = "RussianRouletteLog.txt";

        private NeuralNetworkAnalyzer _analyser;

        public NeuralNetworkAnalyzer analyzer
        {
            get {
                if (_analyser == null)
                {
                    _analyser = new NeuralNetworkAnalyzer();
                    _analyser.TrainNetwork();
                }
                return _analyser; }
            set { _analyser = value; }
        }


        public MainWindow()
        {
            InitializeComponent();
            initialChambers = SafeParseInt(ChambersTextBox.Text, 8);
            initialBullets = SafeParseInt(BulletsTextBox.Text, 7);
            currentBlanks = initialChambers - initialBullets; // Initial number of blanks
            SaveCurrentState(); // Save the initial state
            UpdateProbabilities(); // Automatically calculate on start
            DrawChambers(); // Draw initial state of chambers

             // Initialize neural network
            analyzer.TrainNetwork(); // Train neural network on existing log data
        }

        // Helper method to safely parse integers with validation
        private int SafeParseInt(string value, int defaultValue)
        {
            return int.TryParse(value, out int result) ? result : defaultValue;
        }

        // Save the current state
        private void SaveCurrentState()
        {
            lastChambers = SafeParseInt(ChambersTextBox.Text, 0);
            lastBullets = SafeParseInt(BulletsTextBox.Text, 0);
            lastBlanks = currentBlanks;
        }

        // Restore the last state
        private void RestoreLastState()
        {
            ChambersTextBox.Text = lastChambers.ToString();
            BulletsTextBox.Text = lastBullets.ToString();
            currentBlanks = lastBlanks;
            DrawChambers(); // Update chamber visuals
            UpdateProbabilities(); // Automatically recalculate
        }


        // Log the start of a new game
        private void LogGameStart()
        {
            string logMessage = $"Game Start: {System.DateTime.Now}\n" +
                                $"Chambers: {initialChambers}, Bullets: {initialBullets}\n" +
                                $"-----------------------------------------\n";
            File.AppendAllText(logFilePath, logMessage);

            analyzer.TrainNetwork(); // Re-train the neural network after starting a new game
        }

        // Log the game result (won or lost)
        private void LogGameResult(string result)
        {
            string logMessage = $"Game Result: {result} at {System.DateTime.Now}\n" +
                                $"-----------------------------------------\n";
            File.AppendAllText(logFilePath, logMessage);
        }

        // Button to log a win
        private void LogWon_Click(object sender, RoutedEventArgs e)
        {
            LogGameResult("Won");
        }

        // Button to log a loss
        private void LogLost_Click(object sender, RoutedEventArgs e)
        {
            LogGameResult("Lost");
        }

        // Reset the state to initial values
        private void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            BulletsTextBox.Text = "";
            ChambersTextBox.Text = "";
            currentBlanks = initialChambers - initialBullets;
            SaveCurrentState(); // Save the reset state
            DrawChambers(); // Update chamber visuals
            UpdateProbabilities(); // Recalculate probabilities
            LogGameStart(); // Log the start of a new game
        }

        // Subtract bullet button click handler
        private void SubtractBulletButton_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentState(); // Save the state before making changes

            int chambers = SafeParseInt(ChambersTextBox.Text, 0);
            int bullets = SafeParseInt(BulletsTextBox.Text, 0);

            if (bullets > 0 && chambers > 0)
            {
                bullets--;
                chambers--;
                BulletsTextBox.Text = bullets.ToString();
                ChambersTextBox.Text = chambers.ToString();
                currentBlanks = chambers - bullets; // Update the number of blanks

                DrawChambers(); // Update chamber visuals
                UpdateProbabilities(); // Automatically recalculate

                // Log the round after subtraction
                double hitProbability = ProbabilityOfHit(chambers, bullets);
                LogRound("Bullet Subtracted", bullets, currentBlanks, hitProbability, chambers);
            }
            else
            {
                MessageBox.Show("No more bullets or chambers to subtract.");
            }
        }

        // Subtract blank button click handler
        private void SubtractBlankButton_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentState(); // Save the state before making changes

            int chambers = SafeParseInt(ChambersTextBox.Text, 0);

            if (chambers > 0 && currentBlanks > 0)
            {
                currentBlanks--;
                chambers--;
                ChambersTextBox.Text = chambers.ToString();

                DrawChambers(); // Update chamber visuals
                UpdateProbabilities(); // Automatically recalculate

                // Log the round after subtraction
                double hitProbability = ProbabilityOfHit(chambers, chambers - currentBlanks);
                LogRound("Blank Subtracted", chambers - currentBlanks, currentBlanks, hitProbability, chambers);
            }
            else
            {
                MessageBox.Show("No more blanks or chambers to subtract.");
            }
        }



        // Log each round's details
        private void LogRound(string actionType, int remainingBullets, int remainingBlanks, double hitProbability, int totalChambers)
        {
            string logMessage = $"Round Details: \n" +
                                $"Action: {actionType}\n" +
                                $"Remaining Bullets: {remainingBullets}, Remaining Blanks: {remainingBlanks}\n" +
                                $"Chambers: {totalChambers}\n" +
                                $"Hit Probability: {hitProbability * 100:F2}%\n" +
                                $"-----------------------------\n";

            // Append the log to the file
            File.AppendAllText(logFilePath, logMessage);
        }

        // Remove the last entry from the log when Undo is pressed
        private void RemoveLastLogEntry()
        {
            if (File.Exists(logFilePath))
            {
                var lines = File.ReadAllLines(logFilePath);
                if (lines.Length > 0)
                {
                    // Find the index where the last round starts ("Round Details:")
                    int lastRoundStart = -1;
                    for (int i = lines.Length - 1; i >= 0; i--)
                    {
                        if (lines[i].StartsWith("Round Details:"))
                        {
                            lastRoundStart = i;
                            break;
                        }
                    }

                    if (lastRoundStart != -1)
                    {
                        // Remove the last round and rewrite the file
                        var newLines = lines.Take(lastRoundStart).ToArray();
                        File.WriteAllLines(logFilePath, newLines);
                    }
                }
            }
        }

        // Undo button click handler
        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            isUndoOperation = true; // Indicate that an undo operation is in progress
            RestoreLastState(); // Restore the previous state
            RemoveLastLogEntry(); // Remove the last log entry
            isUndoOperation = false; // Reset the undo operation flag
        }

        // Automatically recalculate and display the probabilities and make prediction
        private void UpdateProbabilities()
        {
            int chambers = SafeParseInt(ChambersTextBox.Text, 0);
            int bullets = SafeParseInt(BulletsTextBox.Text, 0);

            int blanks = chambers - bullets;

            // Validate that bullets and blanks are within limits
            if (bullets < 0 || bullets > initialBullets || blanks < 0)
            {
                MessageBox.Show("Number of bullets or blanks is out of valid range.");
                return;
            }

            // Calculate the probabilities
            double surviveProbability = ProbabilityOfSurvival(chambers, bullets);
            double shotHitProbability = ProbabilityOfHit(chambers, bullets);
            double consecutiveHitProbability = ProbabilityOfConsecutiveHits(chambers, bullets);

            // Display results
            ResultTextBlock.Text = $"Blank: {surviveProbability * 100:F2}%\n" +
                                   $"Hit: {shotHitProbability * 100:F2}%\n" +
                                   $"Next 2 Shots Hit: {consecutiveHitProbability * 100:F2}%";

            // Make prediction using the neural network
            bool betForOdds = analyzer.Predict(bullets, blanks, shotHitProbability * 100);

            // Display the prediction in the UI
            BetSuggestionTextBlock.Text = betForOdds ? "Bet Suggestion: For the Odds" : "Bet Suggestion: Against the Odds";
        }

        private double ProbabilityOfHit(int chambers, int bullets)
        {
            return (double)bullets / chambers;
        }

        private double ProbabilityOfSurvival(int chambers, int bullets)
        {
            return 1 - ProbabilityOfHit(chambers, bullets);
        }

        private double ProbabilityOfConsecutiveHits(int chambers, int bullets)
        {
            double hitFirst = ProbabilityOfHit(chambers, bullets);
            double hitSecond = ProbabilityOfHit(chambers - 1, bullets - 1); // Adjust for second shot
            return hitFirst * hitSecond;
        }

        private void DrawChambers()
        {
            ChambersCanvas.Children.Clear();
            int totalChambers = SafeParseInt(ChambersTextBox.Text, 0);
            int bullets = SafeParseInt(BulletsTextBox.Text, 0);

            double radius = 15;
            double spacing = 30;
            double canvasWidth = ChambersCanvas.ActualWidth;
            double startX = (canvasWidth - (totalChambers * spacing)) / 2;
            double centerY = 50;

            int remainingBlanks = totalChambers - bullets;
            int remainingBullets = bullets;

            // Step 1: Visualize chambers based on dynamic probabilities
            for (int i = 0; i < totalChambers; i++)
            {
                Grid chamberGrid = new Grid
                {
                    Width = 2.8 * radius,
                    Height = 2.8 * radius
                };

                Ellipse circle = new Ellipse
                {
                    Width = 2.8 * radius,
                    Height = 2.8 * radius,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };

                TextBlock percentageText = new TextBlock
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Brushes.White,
                    FontSize = 12 // Adjust as necessary
                };

                double totalRemaining = remainingBullets + remainingBlanks;
                double hitProbability = remainingBullets / totalRemaining;

                if (Math.Abs(hitProbability - 0.5) < 0.01) // Approximately 50%
                {
                    circle.Fill = Brushes.Blue; // Mark blue for 50/50
                }
                else
                {
                    byte redIntensity = (byte)(255 * hitProbability);
                    if (hitProbability > 0.5)
                    {
                        circle.Fill = new SolidColorBrush(Color.FromRgb(redIntensity, 0, 0)); // Red for higher probabilities
                    }
                    else if (hitProbability == 0.0)
                    {
                        circle.Fill = Brushes.Black; // Black if there's no chance of a hit
                    }
                    else
                    {
                        circle.Fill = Brushes.Gray; // Gray for lower probabilities
                    }
                }

                percentageText.Text = $"{hitProbability * 100:F0}%";

                if (remainingBullets > 0 && hitProbability > 0)
                {
                    remainingBullets--; // A hit (bullet) occurred
                }
                else
                {
                    remainingBlanks--; // A blank occurred
                }

                chamberGrid.Children.Add(circle);
                chamberGrid.Children.Add(percentageText);

                Canvas.SetLeft(chamberGrid, startX + i * spacing);
                Canvas.SetTop(chamberGrid, centerY);

                ChambersCanvas.Children.Add(chamberGrid);
            }
        }
        private void ChambersTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Ensure this isn't triggered by an undo operation
            if (isUndoOperation) return;

            int chambers = SafeParseInt(ChambersTextBox.Text, 0);
            int bullets = SafeParseInt(BulletsTextBox.Text, 0);

            // Ensure the chamber count isn't less than the number of bullets
            if (chambers < bullets)
            {
                MessageBox.Show("Number of chambers cannot be less than the number of bullets.");
                return;
            }

            // Update the number of blanks based on new chamber count
            currentBlanks = chambers - bullets;

            // Redraw chambers and recalculate probabilities
            DrawChambers();
            UpdateProbabilities();
        }

        private void BulletsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Ensure this isn't triggered by an undo operation
            if (isUndoOperation) return;

            int chambers = SafeParseInt(ChambersTextBox.Text, 0);
            int bullets = SafeParseInt(BulletsTextBox.Text, 0);

            // Ensure bullets do not exceed chambers or become negative
            if (bullets < 0 || bullets > chambers)
            {
                MessageBox.Show("Number of bullets is invalid.");
                return;
            }

            // Update the number of blanks based on the new bullet count
            currentBlanks = chambers - bullets;

            // Redraw chambers and recalculate probabilities
            DrawChambers();
            UpdateProbabilities();
        }


    }
}
