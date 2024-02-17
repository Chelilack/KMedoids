using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
//using Excel = Microsoft.Office.Interop.Excel;
using System.Linq;


namespace paralelka_try
{
    public class Agent
    {
        public void ForContainers(string[] args)
        {
            int start= 10;
            int m=20 ;
            if (args.Length >= 2)
            {
                start = int.Parse(args[0]);
                m = int.Parse(args[1]);
            }
            else
            {
                Console.WriteLine("Invalid arguments. Usage: KMedoids.dll start m");
            }
            KMedoids kMagent = new KMedoids();
            string filePath = "/app/out.csv";
            //List<Tuple<int, int, double>> data = ReadExcelData(filePath);
            List<Tuple<int, int, double>> data = ReadCsvData(filePath);
            double[,] D = kMagent.CalculateDistanceMatrix(data, false);

            int tmax = 100; 
            ConcurrentDictionary<int, Tuple<int[], Dictionary<int, List<int>>>> results = new ConcurrentDictionary<int, Tuple<int[], Dictionary<int, List<int>>>>();
            ConcurrentDictionary<int, double> metric = new ConcurrentDictionary<int, double>();

            // пример получения диаметра
            double diameter = kMagent.GetDiameter(D);
            Console.WriteLine($"{diameter}");
            /* 
            Parallel.For(1, m + 1, k =>
            {
                var result = kMagent.PerformKMedoids(D, k, tmax);
                results.TryAdd(k, result);
                double maxD = 0;
                
                foreach (var kvp in result.Item2) // учесть, что это в параллельных циклах, избежать ошибок. Все что ниже потенциально опасно 
                {
                    double[,] Dsub = kMedoids.GetSubMatrix(D, kvp.Value) // этого метода пока нет
                    double diameter = kMagent.GetDiameter(Dsub);
                    if (maxD < diameter) {maxD = diameter;}
                }
                
            metric.TryAdd(k,k+maxD);
            });
            */
            Thread[] threads = new Thread[m-start];
            for (int k = 1; k <= m-start; k++)
            {
                int localK = k+start; // Необходимо для передачи локальной переменной в поток
                threads[k - 1] = new Thread(() =>
                {
                    var result = kMagent.PerformKMedoids(D, localK, tmax);
                    results.TryAdd(localK, result);

                    double maxD = 0;
                    
                    foreach (var kvp in result.Item2)
                    {
                        double[,] Dsub = kMagent.GetSubMatrix(D, kvp.Value);

                        double diameter = kMagent.GetDiameter(Dsub);
                        if (maxD < diameter)
                        {
                            maxD = diameter;
                        }
                    }
                    metric.TryAdd(localK, localK + maxD);
                });
                threads[k - 1].Start();
            }

            // Ждем завершения всех потоков
            foreach (var thread in threads)
            {
                thread.Join();
            }
            foreach (var entry in metric)
            {
                Console.WriteLine($"Key: {entry.Key}, Value: {entry.Value}");
            }
            // Tuple<int[], Dictionary<int, List<int>>> result = kMagent.PerformKMedoids(D, 4, 100);

            var result = results[metric.OrderBy(entry => entry.Value).FirstOrDefault().Key];
            ///////////////////////////////////////////////////
            string containerId = Environment.GetEnvironmentVariable("HOSTNAME");

            // Создаем уникальное имя файла, используя идентификатор контейнера
            string fileName = $"result_{containerId}.csv";

            // Записываем результаты в файл
            using (var writer = new StreamWriter($"/app/data/{fileName}", false)) 
            {

                // Записываем индексы медиоидов
                writer.WriteLine("Medoid Indices: " + string.Join(", ", result.Item1));

                // Записываем ассоциации кластеров
                writer.WriteLine("Cluster Associations:");
                foreach (var kvp in result.Item2)
                {
                    writer.WriteLine($"Cluster {kvp.Key}: {string.Join(", ", kvp.Value)}");
                }
            }


            ////////////////////////////////////////////////////
                // выбрать min из metric -> соотнести по k с results

            Console.WriteLine("Medoid Indices: {0}", string.Join(", ", result.Item1));
            Console.WriteLine("Cluster Associations:");
            foreach (var kvp in result.Item2)
            {
                Console.WriteLine($"Cluster {kvp.Key}: {string.Join(", ", kvp.Value)}");
            }
        }
        static void PrintMatrix(double[,] matrix)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    Console.Write(matrix[i, j] + " ");
                }
                Console.WriteLine();
            }
        }
    
        

        public List<Tuple<int, int, double>> ReadCsvData(string filePath)
        {
            List<Tuple<int, int, double>> result = new List<Tuple<int, int, double>>();

            using(var reader = new StreamReader(filePath))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(',');

                    int column1 = int.Parse(values[0]);
                    int column2 = int.Parse(values[1]);
                    double column3 = double.Parse(values[2]);

                    result.Add(Tuple.Create(column1, column2, column3));
                }
            }
            return result;
        }
    }
    
}
