using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace paralelka_try
{

    public class KMedoids
    {

        public Tuple<int[], Dictionary<int, List<int>>> PerformKMedoids(double[,] D, int k, int tmax = 100)
        {
            int n = D.GetLength(0);
            if (k > n)
            {
                throw new Exception("too many medoids");
            }
            
            Console.WriteLine($"Performing KMedoids with k = {k}");

            List<int> validMedoidIndices = Enumerable.Range(0, n).ToList();
            Random rnd = new Random();
            HashSet<int> validMedoidInds = new HashSet<int>(Enumerable.Range(0, n));
            HashSet<int> invalidMedoidInds = new HashSet<int>();
            // Идентификация недопустимых индексов медоидов
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (D[i, j] == 0 && i < j)
                    {
                        invalidMedoidInds.Add(j);
                    }
                }
            }
            List<int> validMedoidList = validMedoidInds.Except(invalidMedoidInds).ToList();
            if (k > validMedoidList.Count)
            {
                throw new Exception($"too many medoids (after removing {invalidMedoidInds.Count} duplicate points)");
            }
            int[] M = validMedoidList.OrderBy(x => rnd.Next()).Take(k).ToArray();
            int[] Mnew = new int[k];
            Array.Copy(M, Mnew, k);
            Dictionary<int, List<int>> C = new Dictionary<int, List<int>>();
            bool shouldStop = false;
            for (int t = 0; t < tmax && !shouldStop; t++)
            {
                // Ассоциация точек данных с ближайшими медоидами
                int[] J = ArgMinParallel(D, M);
                // Обновление медоидов для каждого кластера
                for (int kappa = 0; kappa < k; kappa++)
                {
                    int currentKappa = kappa;
                    C[kappa] = Enumerable.Range(0, J.Length)
                                         .Where(i => J[i] == currentKappa)
                                         .ToList();
                }
                for (int kappa = 0; kappa < k; kappa++)
                {
                    double[] mean= CalculateRowMeans(D, C[kappa]);
                    if (mean.Length > 0)
                    {
                        int j = Array.IndexOf(mean, mean.Min());
                        Mnew[kappa] = C[kappa][j];
                    }
                    else
                    {
                        Console.WriteLine("Warning: Empty cluster. Skipping update.");
                    }
                }
                // Проверка на сходимость
                if (Enumerable.SequenceEqual(M, Mnew))
                {
                    shouldStop = true;
                }
                Array.Copy(Mnew, M, Mnew.Length);
            }
            if (!shouldStop) 
            {
                int[] J = ArgMinParallel(D, M);
                // Обновление медоидов для каждого кластера
                for (int kappa = 0; kappa < k; kappa++)
                {
                    int currentKappa = kappa;
                    C[kappa] = Enumerable.Range(0, J.Length)
                                         .Where(i => J[i] == currentKappa)
                                         .ToList();
                }
            }

            return new Tuple<int[], Dictionary<int, List<int>>>(M, C);
        }
        public int[] ArgMinParallel(double[,] D, int[] M)
        {
            int rows = D.GetLength(0);
            int[] indices = new int[rows];
            Parallel.For(0, rows, i =>
            {
                double minDistance = double.MaxValue;
                int minIndex = -1;
                for (int j = 0; j < M.GetLength(0); j++)
                {
                    if (D[i, M[j]] < minDistance)
                    {
                        minDistance = D[i, M[j]];
                        minIndex = j;
                    }
                }
                indices[i] = minIndex;
            });
            return indices;
        }
        public double[] CalculateRowMeans(double[,] D, List<int> clusterIndexes)
        {
            int numItems = clusterIndexes.Count; // Количество элементов в подматрице
            double[] rowMeans = new double[numItems];

            Parallel.For(0, numItems, i => // Проходимся по строкам подматрицы
            {
                double sum = 0;
                Parallel.For(0, numItems, j => // Проходимся по столбцам подматрицы
                {
                    sum += D[clusterIndexes[i], clusterIndexes[j]];
                });
                rowMeans[i] = sum / numItems;
            });

            return rowMeans;
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
        public double[,] CalculateDistanceMatrix(List<Tuple<int, int, double>> edges, bool IsDirected)
        {
            int VertexCount = edges.Max(tuple => tuple.Item1)+1;
            double[,] DistMatrix = new double[VertexCount, VertexCount];
            Parallel.For(0, VertexCount, i =>
            {
                Parallel.For(0, VertexCount, j =>
                {
                    if (i == j)
                        DistMatrix[i, j] = 0;
                    else
                        DistMatrix[i, j] = double.PositiveInfinity;
                });
            });

            // Заполняем изначальные расстояния на основе рёбер
            foreach (var edge in edges)
            {
                int u = edge.Item1;
                int v = edge.Item2;
                double weight = edge.Item3;

                DistMatrix[u, v] = Math.Min(DistMatrix[u, v], weight);

                if (!IsDirected)
                {
                    DistMatrix[v, u] = Math.Min(DistMatrix[v, u], weight);
                }
            }

            // Алгоритм Флойда-Уоршелла
            for (int k = 0; k < VertexCount; k++)
            {
                for (int i = 0; i < VertexCount; i++)
                {
                    for (int j = 0; j < VertexCount; j++)
                    {
                        if (DistMatrix[i, k] + DistMatrix[k, j] < DistMatrix[i, j])
                        {
                            DistMatrix[i, j] = DistMatrix[i, k] + DistMatrix[k, j];
                        }
                    }
                }
            }
            return DistMatrix;
        }

        // не обработает графы не являющиеся КСС 
        public double GetDiameter(double[,] D)
        {
            int vCount = D.GetLength(0);
            double diameter = 0;

            // Найти max путь
            for (int i = 0; i < vCount; i++)
            {
                for (int j = 0; j < vCount; j++)
                {
                    if (D[i, j] > diameter)
                    {
                        diameter = D[i, j];
                    }
                }
            }

            return diameter;
        }
        public double[,] GetSubMatrix(double[,] originalMatrix, List<int> indices)
        {
            int size = indices.Count;
            double[,] subMatrix = new double[size, size];
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    subMatrix[i, j] = 0;
                }
            }
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    int rowOriginal = indices[i];
                    int colOriginal = indices[j];

                    subMatrix[i, j] = originalMatrix[rowOriginal, colOriginal];
                }
            }

            return subMatrix;
        }
    }
    
}
