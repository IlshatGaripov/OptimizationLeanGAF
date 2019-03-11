using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using QuantConnect.Configuration;
using QuantConnect.Lean.Engine;
using QuantConnect.Logging;
using QuantConnect.Util;
using QuantConnect.Lean.Engine.Results;

namespace Optimization
{
    /// <summary>
    /// Class 4
    /// </summary>
    public class RunClass : MarshalByRefObject
    {
        public decimal Run(ConfigVars vars)
        {
            foreach (KeyValuePair<string, object> kvp in vars.vars)
                Config.Set(kvp.Key, kvp.Value.ToString());

            // settings
            Config.Set("environment", "backtesting");
            Config.Set("algorithm-type-name", "BitfinexSuperTrend");
            Config.Set("algorithm-language", "CSharp");
            Config.Set("algorithm-location", "Optimization.exe");
            Config.Set("data-folder", "C:/Users/stranger/Google Drive/Data/");

            // default value set in QuantConnect.Configuration.Config is invalid. specify explicitely. 
            Config.Set("job-queue-handler", "QuantConnect.Queues.JobQueue");

            // log handler
            Log.LogHandler = Composer.Instance.GetExportedValueByTypeName<ILogHandler>(Config.Get("log-handler", "CompositeLogHandler"));

            // == LeanEngineSystemHandlers == 

            LeanEngineSystemHandlers leanEngineSystemHandlers;
            try
            {
                leanEngineSystemHandlers = LeanEngineSystemHandlers.FromConfiguration(Composer.Instance);
            }
            catch (CompositionException compositionException)
            {
                Log.Error("Engine.Main(): Failed to load library: " + compositionException);
                throw;
            }

            // can this be omitted?
            leanEngineSystemHandlers.Initialize();

            string assemblyPath;
            var job = leanEngineSystemHandlers.JobQueue.NextJob(out assemblyPath);

            if (job == null)
            {
                throw new Exception("Engine.Main(): Job was null.");
            }

            // == LeanEngineAlgorithmHandlers == 

            LeanEngineAlgorithmHandlers leanEngineAlgorithmHandlers;
            try
            {
                leanEngineAlgorithmHandlers = LeanEngineAlgorithmHandlers.FromConfiguration(Composer.Instance);
            }
            catch (CompositionException compositionException)
            {
                Log.Error("Engine.Main(): Failed to load library: " + compositionException);
                throw;
            }

            // == Engine == 

            try
            {
                var liveMode = Config.GetBool("live-mode");
                var algorithmManager = new AlgorithmManager(liveMode);
                // can this be omitted?
                leanEngineSystemHandlers.LeanManager.Initialize(leanEngineSystemHandlers, leanEngineAlgorithmHandlers, job, algorithmManager);
                var engine = new Engine(leanEngineSystemHandlers, leanEngineAlgorithmHandlers, liveMode);
                engine.Run(job, algorithmManager, assemblyPath);
            }
            finally
            {
                // no Acknowledge Job, clean up resources
                Log.Trace("Engine.Main(): Packet removed from queue: " + job.AlgorithmId);
                leanEngineSystemHandlers.Dispose();
                leanEngineAlgorithmHandlers.Dispose();
                Log.LogHandler.Dispose();
            }

            // obtain results
            var sharpeRatio = 0.0m;
            var resultshandler = leanEngineAlgorithmHandlers.Results as BacktestingResultHandler;

            if (resultshandler != null)
            {
                var ratio = resultshandler.FinalStatistics["Sharpe Ratio"];
                Decimal.TryParse(ratio, out sharpeRatio);
            }
            else
            {
                Log.Error("Unable to cast: BacktestingResultHandler");
            }

            return sharpeRatio;
        }

    }
}

