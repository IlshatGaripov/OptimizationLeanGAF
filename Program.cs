using System;
using System.Threading;
using System.Reflection;
using System.Collections.Generic;
using GAF;
using Optimization.GeneticOperators;

namespace Optimization
{
    /// <summary>
    /// Class 1
    /// </summary>
	class MainClass
    {
        private static readonly Random random = new Random();
        private static AppDomainSetup _ads;
        private static string _callingDomainName;
        private static string _exeAssembly;

        /// <summary>
        /// Generates randon double within a given interval
        /// </summary>
        private static double RandomNumberBetween(double minValue, double maxValue)
        {
            var next = random.NextDouble();
            return minValue + (next * (maxValue - minValue));
        }

        /// <summary>
        /// Generates randon in within a given interval
        /// </summary>
        private static int RandomNumberBetweenInt(int minValue, int maxValue)
        {
            return random.Next(minValue, maxValue);
        }

        /// <summary>
        /// Program startup point
        /// </summary>
        public static void Main(string[] args)
        {
            _ads = SetupAppDomain();

            // == GAs params ==
            const double crossoverProbability = 0.65;
            const double mutationProbability = 0.08;
            const int elitismPercentage = 5;

            // create an empty population
            // default ParentSelectionMethod = FitnessProportionateSelection
            var population = new Population();

            // fill in with randomly created chromosomes
            AddChromosomes(20, ref population);

            // create the GA  
            var ga = new GeneticAlgorithm(population, CalculateFitness);

            /*
            // elite
            var elite = new Elite(elitismPercentage);     
            ga.Operators.Add(elite);

            // crossover
            var crossover = new Crossover(crossoverProbability, true)
            {
                CrossoverType = CrossoverType.SinglePoint
            };
            ga.Operators.Add(crossover);
            
            // mutation
            var mutation = new BinaryMutate(mutationProbability, true);
            ga.Operators.Add(mutation);
            */

            // custom operator
            ga.Operators.Add(new ConfigVarsOperator());
            
            // subscribe to the GAs Generation Complete event 
            ga.OnGenerationComplete += ga_OnGenerationComplete;

            // run the GA 
            ga.Run(Terminate);
        }

        /// <summary>
        /// Creates and adds chromosomes to population
        /// </summary>
        /// <param name="size">Number of cromosomes in initial population</param>
        /// <param name="population">Population to fill up</param>
        private static void AddChromosomes(int size, ref Population population)
        {
            // checking for evenness
            if (size % 2 != 0)
            {
                throw new ArgumentException("Population size must be an even number.");
            }

            // create the chromosomes and add to collection
            for (var p = 0; p < size; p++)
            {
                var chromosome = new Chromosome();

                var v = new ConfigVars
                {
                    Vars =
                    {
                        ["bollinger-period"] = RandomNumberBetweenInt(10, 30),
                        ["bollinger-multiplier"] = RandomNumberBetween(1.8, 2.9)
                    }
                };

                chromosome.Genes.Add(new Gene(v));

                population.Solutions.Add(chromosome);
            }
        }

        /// <summary>
        /// Sets up app domain
        /// </summary>
        private static AppDomainSetup SetupAppDomain()
        {
            _callingDomainName = Thread.GetDomain().FriendlyName;
            //Console.WriteLine(callingDomainName);

            // Get and display the full name of the EXE assembly.
            _exeAssembly = Assembly.GetEntryAssembly().FullName;
            //Console.WriteLine(exeAssembly);

            // Construct and initialize settings for a second AppDomain.
            AppDomainSetup ads = new AppDomainSetup();
            ads.ApplicationBase = AppDomain.CurrentDomain.BaseDirectory;

            ads.DisallowBindingRedirects = false;
            ads.DisallowCodeDownload = true;
            ads.ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
            return ads;
        }

        static RunClass CreateRunClassInAppDomain(ref AppDomain ad)
        {
            // Create the second AppDomain.
            var name = Guid.NewGuid().ToString("x");
            ad = AppDomain.CreateDomain(name, null, _ads);

            // Create an instance of MarshalbyRefType in the second AppDomain. 
            // A proxy to the object is returned.
            RunClass rc = (RunClass)ad.CreateInstanceAndUnwrap(_exeAssembly, typeof(RunClass).FullName);

            return rc;
        }

        static void ga_OnRunComplete(object sender, GaEventArgs e)
        {
            var fittest = e.Population.GetTop(1)[0];
            foreach (var gene in fittest.Genes)
            {
                ConfigVars v = (ConfigVars)gene.ObjectValue;
                foreach (KeyValuePair<string, object> kvp in v.Vars)
                    Console.WriteLine("Variable {0}:, value {1}", kvp.Key, kvp.Value.ToString());
            }
        }

        private static void ga_OnGenerationComplete(object sender, GaEventArgs e)
        {
            var fittest = e.Population.GetTop(1)[0];
            var sharpe = RunAlgorithm(fittest);
            Console.WriteLine("Generation: {0}, Fitness: {1},sharpe: {2}", e.Generation, fittest.Fitness, sharpe);
        }
        
        /// <summary>
        /// This summaries conditions for GA to complete execution.
        /// </summary>
        public static bool Terminate(Population population, int currentGeneration, long currentEvaluation)
        {
            return currentGeneration > 3;
        }

        /// <summary>
        /// Fitness function used for evaluation in GA 
        /// </summary>
        public static double CalculateFitness(Chromosome chromosome)
        {
            var sharpe = RunAlgorithm(chromosome);
            return sharpe;
        }

        /// <summary>
        /// This is called inside CalculateFitness
        /// </summary>
        private static double RunAlgorithm(Chromosome chromosome)
        {
            var sumSharpe = 0.0;
            var i = 0;
            foreach (var gene in chromosome.Genes)
            {
                Console.WriteLine("Running gene number {0}", i);
                var val = (ConfigVars)gene.ObjectValue;
                AppDomain ad = null;
                RunClass rc = CreateRunClassInAppDomain(ref ad);
                foreach (KeyValuePair<string, object> kvp in val.Vars)
                    Console.WriteLine("Running algorithm with variable {0}:, value {1}", kvp.Key, kvp.Value.ToString());

                var res = (double)rc.Run(val);
                Console.WriteLine("Sharpe ratio: {0}", res);
                sumSharpe += res;
                AppDomain.Unload(ad);
                Console.WriteLine("Sum Sharpe ratio: {0}", sumSharpe);

                i++;
            }

            return sumSharpe;
        }
    }
    
    /// <summary>
    /// Represents that algorithm input parameters. Passed and retrieved via Lean's Config class.
    /// </summary>
	[Serializable]
	public class ConfigVars
	{
		public readonly Dictionary<string,object> Vars = new  Dictionary<string, object> ();

		public override bool Equals(object obj) 
		{ 
			var item = obj as ConfigVars; 
			return Equals(item); 
		} 

		protected bool Equals(ConfigVars other) 
		{ 
			foreach (var kvp in Vars) {
				if (kvp.Value.ToString () != other.Vars [kvp.Key].ToString ())
					return false;
			}
			return true;
		} 

		public override int GetHashCode() 
		{ 
			unchecked 
			{ 
				var hashCode = 0;
				foreach (var kvp in Vars)
					hashCode = hashCode * kvp.Value.GetHashCode ();
				return hashCode; 
			} 
		} 
	}

}

