using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

            var cts = new CancellationTokenSource();
            var integralCalculator = new IntegralCalculator();

            var tasks = intervals.Select(interval =>
                Task.Run(() => integralCalculator.Calculate(selectedFunction, selectedFunctionName, interval, cts.Token), cts.Token)).ToList();

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
                Task.WaitAll(tasks.ToArray());

                Console.WriteLine("\nPodsumowanie obliczeń:");
                foreach (var task in tasks)
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        var (result, summary) = task.Result;
                        Console.WriteLine($"{summary} Wynik: {result:F4}");
                    }
                }
            }
            catch (AggregateException ex)
            {
                if (ex.InnerExceptions.Any(e => e is OperationCanceledException))
                {
                    Console.WriteLine("Obliczenia zostały anulowane.");
                }
                else
                {
                    Console.WriteLine("Wystąpił błąd: " + ex.Message);
                }
            }
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