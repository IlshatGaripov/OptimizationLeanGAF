using System;
using System.Linq;
using GAF;

namespace Optimization.GeneticOperators
{
    /// <summary>
    /// Custom implementation of genetic Oprator. by Christoffer Soerensen (c).
    /// </summary>
    public class ConfigVarsOperator : IGeneticOperator
    {
        private int _invoked = 0;
        private readonly Random _rand = new Random();

        public ConfigVarsOperator()
        {
            Enabled = true;
        }

        public void Invoke(Population currentPopulation, ref Population newPopulation, FitnessFunction fitnesFunctionDelegate)
        {
            //take top 3 
            const int num = 3;
            var min = System.Math.Min(num, currentPopulation.Solutions.Count);

            var best = currentPopulation.GetTop(min);
            // uses Fitness
            var cutoff = best[min - 1].Fitness;
            var genecount = best[0].Genes.Count;

            try
            {
                var configVars = (ConfigVars)best[_rand.Next(0, min - 1)].Genes[_rand.Next(0, genecount - 1)].ObjectValue;
                var index = _rand.Next(0, configVars.Vars.Count - 1);
                var key = configVars.Vars.ElementAt(index).Key;
                newPopulation.Solutions.Clear();
                foreach (var chromosome in currentPopulation.Solutions)
                {
                    if (chromosome.Fitness < cutoff)
                    {
                        foreach (var gene in chromosome.Genes)
                        {
                            var targetConfigVars = (ConfigVars)gene.ObjectValue;
                            targetConfigVars.Vars[key] = configVars.Vars[key];
                        }
                    }
                    newPopulation.Solutions.Add(chromosome);
                }

                _invoked++;
            }
            catch (Exception e)
            {
                Console.WriteLine("OOPS! " + e.Message + " " + e.StackTrace);
            }
        }

        public int GetOperatorInvokedEvaluations()
        {
            return _invoked;
        }

        public bool Enabled { get; set; }

        /// <summary>
        /// New interface property. Since 2.3.1 version of library.
        /// </summary>
        public bool RequiresEvaluatedPopulation { get; set; } = true;
    }
}
