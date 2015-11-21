using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Http;
using System.Data;
using System.Net;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;


namespace IndexQuotationService
{
    class Program
    {
        public static Queue<Thread> Threads;
        public static bool res
        {
            get
            {
                return res;
            }
            set
            {
                if (value)
                    for (int i = 0; i < IndexQuotationService.Program.Threads.Count; i++)
                        IndexQuotationService.Program.Threads.ElementAt(i).Interrupt();                        // KILL ALL OUR Threads * Here work fucking slowly*
            }
        }
        static void Main(string[] args)
        {
            String[] adresses = System.IO.File.ReadAllLines("ip.txt");         // read ip's from file
            String[] ip = new String[adresses.Length];
            String[] ports = new String[adresses.Length];

            Threads = new Queue<Thread>();
            // Проходим по адресам
            for (int i = 0; i < adresses.Length - 1; i++)
            {
                String[] result = adresses[i].Split(new Char[] { ':' });
                ip[i] = result[0].ToString();
                ports[i] = result[1].ToString();
            }
            int q = 0;
            while (!res)
            {
                if (q < adresses.Length - 1)
                {
                    string currip = ip[q], currrport = ports[q];
                   
                    // Create  but do not start it.
                    Thread current = new Thread(new ThreadStart(delegate()
                    {
                        try
                        {
                            var g = new GoogleScholarDataGetter("https://scholar.google.com.ua/citations?user=b18TCTYAAAAJ&hl=uk", ref currip, ref currrport);
                            // Вызываем парсер
                            g.executeParser();
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                        return;

                    }));

                    current.Name = "Worker " + q.ToString();
                    current.IsBackground = true;
                    current.Priority = ThreadPriority.Highest;
                    Threads.Enqueue(current);
                    Threads.Last().Start();
                    q++;

                  
                }
            }






        }
    }
}


    

