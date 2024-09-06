using Accord.Neuro.Learning;
using Accord.Neuro;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Accord.Statistics.Analysis;

namespace BuckshotRouletteHelper
{
    public class NeuralNetworkAnalyzer
    {
        private ActivationNetwork network;
        private const string LogFilePath = "RussianRouletteLog.txt";

        public NeuralNetworkAnalyzer()
        {
            // Create a network with 3 inputs (Remaining Bullets, Remaining Blanks, Hit Probability)
            // 2 hidden neurons and 1 output neuron (prediction: bet for or against the odds)
            network = new ActivationNetwork(
                new SigmoidFunction(), // Activation function
                3,                     // 3 inputs
                2,                     // 2 neurons in hidden layer
                1                      // 1 output (bet for or against odds)
            );

            // Initialize the weights of the network
            new NguyenWidrow(network).Randomize();
        }

        // Parse log file and return input/output pairs
        public List<Tuple<double[], double>> ParseLogFile()
        {
            List<Tuple<double[], double>> trainingData = new List<Tuple<double[], double>>();
            if (!File.Exists(LogFilePath))
                throw new FileNotFoundException("Log file not found.");

            var lines = File.ReadAllLines(LogFilePath);

            // Temporary storage for each game
            List<double[]> gameData = new List<double[]>();
            double gameResult = 0;  // 1 for Won, 0 for Lost

            foreach (var line in lines)
            {
                if (line.StartsWith("Round Details"))
                {
                    // Extract relevant data from the rounds
                    int remainingBullets = ExtractIntValue(lines, "Remaining Bullets");
                    int remainingBlanks = ExtractIntValue(lines, "Remaining Blanks");
                    double hitProbability = ExtractDoubleValue(lines, "Hit Probability");

                    // Add the extracted data as a training input (normalize the data between 0 and 1)
                    gameData.Add(new double[]
                    {
                        remainingBullets / 6.0,  // Normalize bullets
                        remainingBlanks / 6.0,   // Normalize blanks
                        hitProbability / 100.0   // Hit probability as percentage
                    });
                }
                else if (line.StartsWith("Game Result"))
                {
                    gameResult = line.Contains("Won") ? 1.0 : 0.0; // 1 if "Won", 0 if "Lost"

                    // Add all the rounds for the game as training data with the game result
                    foreach (var input in gameData)
                    {
                        trainingData.Add(new Tuple<double[], double>(input, gameResult));
                    }

                    // Clear data for next game
                    gameData.Clear();
                }
            }

            return trainingData;
        }

        private int ExtractIntValue(string[] lines, string key)
        {
            var line = lines.FirstOrDefault(l => l.Contains(key));

            if (line != null)
            {
                // Split the line at the colon
                string[] parts = line.Split(':');

                if (parts.Length > 1)
                {
                    // Extract the part after the colon and take the first numeric portion
                    string numericPart = parts[1].Split(',')[0].Trim(); // Split by comma and trim whitespace
                    if (int.TryParse(numericPart, out int result))
                    {
                        return result;
                    }
                }
            }

            // Default return 0 if parsing fails or line is not found
            return 0;
        }


        private double ExtractDoubleValue(string[] lines, string key)
        {
            var line = lines.FirstOrDefault(l => l.Contains(key));
            return line != null ? double.Parse(line.Split(':')[1].Trim().Trim('%')) : 0.0;
        }

        // Train the network using backpropagation
        public void TrainNetwork()
        {
            var trainingData = ParseLogFile();

            // Prepare inputs and expected outputs
            double[][] inputs = trainingData.Select(t => t.Item1).ToArray();
            double[][] outputs = trainingData.Select(t => new double[] { t.Item2 }).ToArray();

            // Create teacher for network
            var teacher = new BackPropagationLearning(network)
            {
                LearningRate = 0.1,
                Momentum = 0.1
            };

            // Train the network for 1000 epochs
            for (int i = 0; i < 1000; i++)
            {
                double error = teacher.RunEpoch(inputs, outputs);
                Console.WriteLine($"Epoch {i + 1}, Error: {error}");
                if (error < 0.01)
                    break;
            }
        }

        // Predict whether to bet against or for the odds
        public bool Predict(double remainingBullets, double remainingBlanks, double hitProbability)
        {
            // Normalize inputs
            double[] input = new double[]
            {
                remainingBullets / 6.0,
                remainingBlanks / 6.0,
                hitProbability / 100.0
            };

            // Get prediction (output is between 0 and 1)
            double[] output = network.Compute(input);
            return output[0] >= 0.5; // Return true if for the odds (>= 50% confidence), otherwise false
        }
    }
}
