using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace NumericalIntegration
{
    class Program
    {
        static void Main(string[] args)
        {
            var functionSelector = new FunctionSelector();
            var selectedFunction = functionSelector.SelectFunction(out string selectedFunctionName);

            var intervalManager = new IntervalManager();
            var intervals = intervalManager.GetIntervals();

            Console.WriteLine("Wybierz metodę przetwarzania:");
            Console.WriteLine("1: TPL");
            Console.WriteLine("2: Thread");
            Console.WriteLine("3: ThreadPool");
            int methodChoice = int.Parse(Console.ReadLine());

            IProcessingMethod processingMethod = methodChoice switch
            {
                1 => new TPLProcessingMethod(),
                2 => new ThreadProcessingMethod(),
                3 => new ThreadPoolProcessingMethod(),
                _ => throw new InvalidOperationException("Nieprawidłowy wybór.")
            };

            var cts = new CancellationTokenSource();
            var stopwatch = Stopwatch.StartNew();

            Console.WriteLine("Naciśnij 'q', aby przerwać obliczenia.");
            Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q)
                    {
                        cts.Cancel();
                        Console.WriteLine("Przerwano obliczenia.");
                    }
                }
            });

            try
            {
                processingMethod.Process(selectedFunction, selectedFunctionName, intervals, cts);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Obliczenia zostały anulowane.");
            }

            stopwatch.Stop();
            Console.WriteLine($"Całkowity czas wykonania: {stopwatch.Elapsed}");
        }
    }

    public interface IProcessingMethod
    {
        void Process(IFunction function, string functionName, List<(double a, double b, int n)> intervals, CancellationTokenSource cts);
    }

    public class TPLProcessingMethod : IProcessingMethod
    {
        public void Process(IFunction function, string functionName, List<(double a, double b, int n)> intervals, CancellationTokenSource cts)
        {
            var tasks = intervals.Select(interval =>
                Task.Run(() => new IntegralCalculator().Calculate(function, functionName, interval, cts.Token), cts.Token)).ToList();

            Task.WaitAll(tasks.ToArray());
            Console.WriteLine("\nPodsumowanie obliczeń TPL:");
            foreach (var task in tasks)
            {
                if (task.IsCompletedSuccessfully)
                {
                    var (result, summary) = task.Result;
                    Console.WriteLine($"{summary} Wynik: {result:F4}");
                }
            }
        }
    }

    public class ThreadProcessingMethod : IProcessingMethod
    {
        public void Process(IFunction function, string functionName, List<(double a, double b, int n)> intervals, CancellationTokenSource cts)
        {
            var threads = new List<Thread>();
            foreach (var interval in intervals)
            {
                var thread = new Thread(() =>
                {
                    var (result, summary) = new IntegralCalculator().Calculate(function, functionName, interval, cts.Token);
                    Console.WriteLine($"{summary} Wynik: {result:F4}");
                });
                threads.Add(thread);
                thread.Start();
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }
        }
    }

    public class ThreadPoolProcessingMethod : IProcessingMethod
    {
        public void Process(IFunction function, string functionName, List<(double a, double b, int n)> intervals, CancellationTokenSource cts)
        {
            var waitHandles = new List<ManualResetEvent>();
            foreach (var interval in intervals)
            {
                var resetEvent = new ManualResetEvent(false);
                waitHandles.Add(resetEvent);

                ThreadPool.QueueUserWorkItem(state =>
                {
                    var (result, summary) = new IntegralCalculator().Calculate(function, functionName, interval, cts.Token);
                    Console.WriteLine($"{summary} Wynik: {result:F4}");
                    resetEvent.Set();
                });
            }

            WaitHandle.WaitAll(waitHandles.ToArray());
        }
    }

    public interface IFunction
    {
        double Evaluate(double x);
        string Name { get; }
    }

    public class Function1 : IFunction
    {
        public double Evaluate(double x) => 2 * x + 2 * Math.Pow(x, 2);
        public string Name => "y = 2x + 2x^2";
    }

    public class Function2 : IFunction
    {
        public double Evaluate(double x) => 2 * Math.Pow(x, 2) + 3;
        public string Name => "y = 2x^2 + 3";
    }

    public class Function3 : IFunction
    {
        public double Evaluate(double x) => 3 * Math.Pow(x, 2) + 2 * x - 3;
        public string Name => "y = 3x^2 + 2x - 3";
    }

    public class FunctionSelector
    {
        private readonly List<IFunction> _functions = new()
        {
            new Function1(),
            new Function2(),
            new Function3()
        };

        public IFunction SelectFunction(out string functionName)
        {
            Console.WriteLine("Wybierz funkcję:");
            for (int i = 0; i < _functions.Count; i++)
            {
                Console.WriteLine($"{i + 1}: {_functions[i].Name}");
            }

            int choice;
            while (true)
            {
                Console.Write("Twój wybór (1-3): ");
                if (int.TryParse(Console.ReadLine(), out choice) && choice >= 1 && choice <= _functions.Count)
                    break;
                Console.WriteLine("Nieprawidłowy wybór. Spróbuj ponownie.");
            }

            var selectedFunction = _functions[choice - 1];
            functionName = selectedFunction.Name;
            return selectedFunction;
        }
    }

    public class IntervalManager
    {
        public List<(double a, double b, int n)> GetIntervals()
        {
            Console.Write("Podaj liczbę przedziałów: ");
            int intervalCount = int.Parse(Console.ReadLine());

            var intervals = new List<(double a, double b, int n)>();

            for (int i = 0; i < intervalCount; i++)
            {
                Console.Write($"Przedział {i + 1} - początek: ");
                double a = double.Parse(Console.ReadLine());

                Console.Write($"Przedział {i + 1} - koniec: ");
                double b = double.Parse(Console.ReadLine());

                Console.Write($"Przedział {i + 1} - liczba podziałów: ");
                int n = int.Parse(Console.ReadLine());

                intervals.Add((a, b, n));
            }

            return intervals;
        }
    }

    public class IntegralCalculator
    {
        public (double, string) Calculate(IFunction f, string functionName, (double a, double b, int n) interval, CancellationToken token)
        {
            double a = interval.a;
            double b = interval.b;
            int n = interval.n;

            double h = (b - a) / n;
            double sum = 0.5 * (f.Evaluate(a) + f.Evaluate(b));

            for (int i = 1; i < n; i++)
            {
                token.ThrowIfCancellationRequested();
                if (i % (n / 10) == 0)
                {
                    Console.WriteLine($"[Przedział {a}-{b}] Postęp: {(double)i / n:P0}");
                }
                sum += f.Evaluate(a + i * h);
                Thread.Sleep(10);
            }

            string summary = $"Funkcja: {functionName}, Przedział: [{a}, {b}], Metoda: trapezy, Podziały: {n}.";
            return (sum * h, summary);
        }
    }
}
