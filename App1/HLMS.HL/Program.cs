using System;
using System.Linq;
using System.Configuration;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.MediaServices.Client;

namespace HLMS.HL
{
    class Program
    {
        private static CloudMediaContext _context = null;
        private static readonly string _supportFiles =
            Path.GetFullPath(@"../..\supportFiles");
        private const string _mpName = "Azure Media Hyperlapse";


        // Paths to support files (within the above base path). You can use 
        // the provided sample media files from the "supportFiles" folder, or 
        // provide paths to your own media files below to run these samples.
        private static readonly string _inputFile =
            Path.GetFullPath(_supportFiles + @"\multifile\interview2.wmv");
        private static readonly string _outputFolder =
            Path.GetFullPath(_supportFiles + @"\outputfiles");
        private const string _hyperlapseConfiguration = "C:\\mediaservies\\samples\\Windows Azure Media Services Getting Started Sample\\C#\\Hyperlapse\\hyperlapseconfig.xml";



        private static readonly string _accountKey = ConfigurationManager.AppSettings["accountKey"];
        private static readonly string _accountName = ConfigurationManager.AppSettings["accountName"];
        static void Main(string[] args)
        {
            _context = new CloudMediaContext(_accountName, _accountKey);
            RunHyperlapseJob(_inputFile, _outputFolder, _hyperlapseConfiguration);
        }

        static void RunHyperlapseJob(string input, string output, string hyperConfig)
        {
            // create asset with input file
            IAsset asset = _context
                           .Assets
                           .CreateFromFile(input, AssetCreationOptions.None);

            // grab instance of Azure Media Hyperlapse MP
            IMediaProcessor mp = _context
                                 .MediaProcessors
                                 .GetLatestMediaProcessorByName(_mpName);

            // create Job with hyperlapse task
            IJob job = _context
                       .Jobs
                       .Create(String.Format("Hyperlapse {0}", input));

            if (!String.IsNullOrEmpty(hyperConfig))
            {
                hyperConfig = File.ReadAllText(hyperConfig);
            }
            ITask hyperlapseTask = job.Tasks.AddNew("Hyperlapse task",
                                                    mp,
                                                    hyperConfig,
                                                    TaskOptions.None);
            hyperlapseTask.InputAssets.Add(asset);
            hyperlapseTask.OutputAssets.AddNew("Hyperlapse output",
                                                AssetCreationOptions.None);


            job.Submit();

            // Create progress printing and querying tasks
            Task progressPrintTask = new Task(() =>
            {

                IJob jobQuery = null;
                do
                {
                    var progressContext = _context;
                    jobQuery = progressContext.Jobs
                                              .Where(j => j.Id == job.Id)
                                              .First();
                    Console.WriteLine(string.Format("{0}\t{1}\t{2}",
                                      DateTime.Now,
                                      jobQuery.State,
                                      jobQuery.Tasks[0].Progress));
                    Thread.Sleep(10000);
                }
                while (jobQuery.State != JobState.Finished &&
                       jobQuery.State != JobState.Error &&
                       jobQuery.State != JobState.Canceled);
            });
            progressPrintTask.Start();

            Task progressJobTask = job.GetExecutionProgressTask(
                                                 CancellationToken.None);
            progressJobTask.Wait();

            // If job state is Error, the event handling 
            // method for job progress should log errors.  Here we check 
            // for error state and exit if needed.
            if (job.State == JobState.Error)
            {
                ErrorDetail error = job.Tasks.First().ErrorDetails.First();
                Console.WriteLine(string.Format("Error: {0}. {1}",
                                                error.Code,
                                                error.Message));
            }

            DownloadAsset(job.OutputMediaAssets.First(), output);
        }

        static void DownloadAsset(IAsset asset, string outputDirectory)
        {
            foreach (IAssetFile file in asset.AssetFiles)
            {
                file.Download(Path.Combine(outputDirectory, file.Name));
            }
        }

        // event handler for Job State
        static void StateChanged(object sender, JobStateChangedEventArgs e)
        {
            Console.WriteLine("Job state changed event:");
            Console.WriteLine("  Previous state: " + e.PreviousState);
            Console.WriteLine("  Current state: " + e.CurrentState);
            switch (e.CurrentState)
            {
                case JobState.Finished:
                    Console.WriteLine();
                    Console.WriteLine("Job finished.");
                    break;
                case JobState.Canceling:
                case JobState.Queued:
                case JobState.Scheduled:
                case JobState.Processing:
                    Console.WriteLine("Please wait...\n");
                    break;
                case JobState.Canceled:
                    Console.WriteLine("Job is canceled.\n");
                    break;
                case JobState.Error:
                    Console.WriteLine("Job failed.\n");
                    break;
                default:
                    break;
            }
        }
    }
}