using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Linq;
using System.Collections.Concurrent;
namespace paralelka_try
{
    class Manager
    {
        static void Main(string[] args)
        {
            Agent agent = new Agent();
            KMedoids kMedoids = new KMedoids();
            if (args.Length > 0)
            {
                string firstArg = args[0];
                if (firstArg == "ForContainers")
                {
                    agent.ForContainers(args.Skip(1).ToArray());
                }
            }
            else
            {
                // Путь к вашему Docker Compose файлу
                string composeFilePath = @"C:\Users\alesh\source\repos\KMedoids\docker-compose.yml";
                string x1 = "0";
                string x2 = "20"; // k/3
                string x3 = "40"; // (k/3)*2
                string x4 = "60"; // k

                // Создание процесса для запуска Docker Compose
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "docker-compose",
                    Arguments = $"-f {composeFilePath} up -d --build",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    Environment =
                {
                    ["x1"] = x1,
                    ["x2"] = x2,
                    ["x3"] = x3,
                    ["x4"] = x4
                }
                };

                // Запуск процесса
                using (Process process = new Process { StartInfo = startInfo })
                {
                    process.Start();

                    // Чтение вывода процесса
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        Console.WriteLine($"Docker Compose started successfully with parameters: {x1}, {x2}, {x3}, {x4}");
                    }
                    else
                    {
                        Console.WriteLine($"Docker Compose failed to start with parameters: {x1}, {x2}, {x3}, {x4}. Error: {error}");
                    }
                }

                string directoryPath = @"D:/volume";

                var csvFiles = Directory.GetFiles(directoryPath, "*.csv");

                Dictionary<int, Tuple<int[], Dictionary<int, List<int>>>> results = new Dictionary<int, Tuple<int[], Dictionary<int, List<int>>>>();
                List<int> medoidIndices = new List<int>();
                // Чтение каждого CSV-файла
                foreach (var csvFile in csvFiles)
                {
                    try
                    {
                        var lines = File.ReadAllLines(csvFile);

                        foreach (var line in lines)
                        {
                            if (line.StartsWith("Medoid Indices:"))
                            {
                                medoidIndices = line.Substring("Medoid Indices:".Length).Trim().Split(',').Select(s => int.Parse(s.Trim())).ToList();

                            }
                            else if (line.StartsWith("Cluster Associations:"))
                            {
                                var clusterAssociations = new Dictionary<int, List<int>>();
                                for (int i = 2; i < lines.Length; i++)
                                {
                                    var clusterLine = lines[i];
                                    if (clusterLine.StartsWith("Cluster"))
                                    {
                                        var parts = clusterLine.Split(':');
                                        var clusterIndex = int.Parse(parts[0].Substring("Cluster".Length).Trim());

                                        var clusterMembers = parts[1].Trim().Split(',').Select(s => int.Parse(s.Trim())).ToList();
                                        clusterAssociations[clusterIndex] = clusterMembers;
                                    }
                                }
                                results.Add(medoidIndices.Count, new Tuple<int[], Dictionary<int, List<int>>>(medoidIndices.ToArray(), clusterAssociations));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading file {csvFile}: {ex.Message}");
                    }
                }
                foreach (var result in results.Values)
                {
                    Console.WriteLine("Medoid Indices: " + string.Join(", ", result.Item1));
                    Console.WriteLine("Cluster Associations:");
                    foreach (var kvp in result.Item2)
                    {
                        Console.WriteLine($"Cluster {kvp.Key}: {string.Join(", ", kvp.Value)}");
                    }
                    Console.WriteLine("-----------------------");
                }
                string filePath = @"C:/Users/alesh/source/repos/KMedoids/out.csv";
                List<Tuple<int, int, double>> data = agent.ReadCsvData(filePath);
                ConcurrentDictionary<int, double> metricResult = new ConcurrentDictionary<int, double>();
                double[,] D = kMedoids.CalculateDistanceMatrix(data, false);
                double maxD = 0;
                foreach (var result in results.Values)
                {
                    foreach (var kvp in result.Item2)
                    {
                        double[,] Dsub = kMedoids.GetSubMatrix(D, kvp.Value);
                        double diameter = kMedoids.GetDiameter(Dsub);
                        if (maxD < diameter)
                        {
                            maxD = diameter;
                        }
                    }
                    metricResult.TryAdd(result.Item1.Length, result.Item1.Length + maxD);
                }
                var answer = results[metricResult.OrderBy(entry => entry.Value).FirstOrDefault().Key];
                Console.WriteLine("Medoid Indices: {0}", string.Join(", ", answer.Item1));
                Console.WriteLine("Cluster Associations:");
                foreach (var kvp in answer.Item2)
                {
                    Console.WriteLine($"Cluster {kvp.Key}: {string.Join(", ", kvp.Value)}");
                }
            }
           
        }
    }
}
