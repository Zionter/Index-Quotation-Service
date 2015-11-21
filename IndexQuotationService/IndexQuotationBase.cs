using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.IO;
using System.Net;
using HtmlAgilityPack;
using UnidecodeSharpFork;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace IndexQuotationService
{

    //        для теста:

    //        string res = string.Empty;
    //        string[] arrayRes;

    //        GoogleCholarDataGetter getter = new GoogleScholarDataGetter("Глазунова Олена");
    //        getter.executeParser();

    //        форматированная строка
    //        res += getter.OutputDataToString;

    //        строка массив
    //        arrayRes = getter.OutputData;


    ///<summary>
    /// Класс работы с scholar.google 
    ///</summary>
    public partial class GoogleScholarDataGetter
    {
        #region Поля
        // имя юзера
        private string userName;
        private string url;
        string proxyIP { get; set; }
        int proxyPort { get; set; }

        // части урл запроса для выдирания ссылки на профиль юзера через поисковый запрос
        const string googleSearchUrlPrefix = "https://scholar.google.com.ua/scholar?hl=uk&q=";
        const string googleSearchUrlAffix = "&btnG=";

        // части урл запроса для получения контента из профиля юзера
        const string googleProfileUrlPrefix = "https://scholar.google.com.ua/citations?user=";
        const string googleProfileUlrAffix = "&hl=uk&oi=ao";

        // возвращаемый результат массивом
        private string[] returnedOriginalData;
        public string[] OutputData
        {
            get
            {
                if (this.returnedOriginalData != null)
                    return this.returnedOriginalData;

                return new string[] { "null" };
            }
        }

        // возвращаемый результат одной строкой (дебаг)
        private string returnedSplitData;
        public string OutputDataToString
        {
            get
            {
                if (this.returnedSplitData != null)
                    return this.returnedSplitData;

                return "null";
            }
        }

        #endregion

        // -------------------
        public GoogleScholarDataGetter(string userName)
        {
            this.userName = userName;
        }


        public GoogleScholarDataGetter(string userName, ref String ip, ref String ports)
        {
            this.userName = userName;
            this.proxyIP = ip;
            this.proxyPort = int.Parse(ports);
        }

        public GoogleScholarDataGetter(string url, string ip, string ports)
        {
            this.url = url;
            this.proxyIP = ip;
            this.proxyPort = int.Parse(ports);
        }


        public static string[,] result { get; set; }

       /// <summary>
       ///  get data from page
       /// </summary>
       /// <param name="args"></param>
       /// <returns> object</returns>
        static async Task<object> DoWorkAsync(object[] args)
        {
            //  Console.WriteLine("Start working {0:d}", Thread.CurrentThread.ManagedThreadId);
            string[] res = args[0] as string[];
            string url = res[0];
            string pip = String.Empty;
            if (!string.IsNullOrWhiteSpace(res[1]))
                pip = res[1];

            string pprt = String.Empty;
            if (!string.IsNullOrWhiteSpace(res[2]))
                pprt = res[2];

            var wb = new WebBrowser();
            {
                wb.ScriptErrorsSuppressed = true;

                TaskCompletionSource<bool> tcs = null;
                WebBrowserDocumentCompletedEventHandler documentCompletedHandler = (s, e) =>
                    tcs.TrySetResult(true);

                tcs = new TaskCompletionSource<bool>();
                wb.DocumentCompleted += documentCompletedHandler;
                try
                {
                    if (!string.IsNullOrWhiteSpace(pip))
                    {
                        Proxy.Set(new WebProxy(pip, int.Parse(pprt)));
                    }

                    wb.Navigate(url.ToString());
                    // await for DocumentCompleted
                    await tcs.Task;
                }
                finally
                {
                    wb.DocumentCompleted -= documentCompletedHandler;
                }
                // the DOM is ready
                // Console.WriteLine(url.ToString());


                HtmlElement[] tr = new HtmlElement[3];
                string[,] td = new string[3, 3];
                try
                {
                    var t = wb.Document.GetElementById("gsc_rsb_st");
                    if (t != null)
                    {
                        foreach (HtmlElement element in t.Children)
                        {
                            if (element.TagName == "TBODY")
                            {
                                for (int i = 1; i < element.Children.Count; i++)
                                {
                                    tr[i - 1] = element.Children[i];
                                }
                                for (int i = 0; i < tr.Length; i++)
                                {
                                    if (tr[i].TagName == "TR")
                                    {
                                        for (int k = 0; k < tr[i].Children.Count; k++)
                                        {
                                            td[i, k] = tr[i].Children[k].InnerText;
                                        }
                                    }
                                }
                            }
                        }
                        Console.WriteLine("ip : {0:n} result:{1:n}", pip, td[0, 1].ToString());
                        return td;
                    }
                    else
                        return null;
                }
                catch (Exception)
                {
                    return null;
                }

            }
        }






        /// <summary>
        // Парсит профиль и достает построчно данные из таблицы.
        /// </summary>
        /// <returns> list with data</returns>
        public ArrayList getDataFromPersonalPage()
        {
            ArrayList res = new ArrayList();

            /// IF CALLED WITH PROXY 
            if (!string.IsNullOrWhiteSpace(proxyIP))
            {
                var task = MessageLoopWorker.Run(DoWorkAsync,
                new string[] { userName, proxyIP, proxyPort.ToString() } as object);
                task.Wait();
                result = (String[,])task.Result;
                if (task.Result != null)
                {
                    Console.WriteLine("DoWorkAsync completed succesfull. ip:{0:n}", proxyIP);
                    for (int i = 0; i < 3; i++)
                        for (int j = 0; j < 3; j++)
                            res.Add(result[i, j]);
                    return res;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                throw new ArgumentNullException();
            }

        }



        // -------------------
        // главный метод, обязателен к вызову.
        // -------------------
        public void executeParser()
        {
            try
            {

                ArrayList userProfileRequest = getDataFromPersonalPage();                    // get data from page
                String[] stringArray = (String[])userProfileRequest.ToArray(typeof(String)); // here we will operate with data later 
                IndexQuotationService.Program.res = true;                                    // set flag which stops all threads
            }
            catch (Exception)
            {
                Console.WriteLine("DoWorkAsync completed unsuccesfull. ip:{0:n}", proxyIP);
                return;
            }
        }




        // a helper class to start the message loop and execute an asynchronous task      "Спижжено  с нета, понимание написаного лишь базовое"
        public static class MessageLoopWorker
        {
            public static async Task<object> Run(Func<object[], Task<object>> worker, params object[] args)
            {
                var tcs = new TaskCompletionSource<object>();

                var thread = new Thread(() =>
                {
                    EventHandler idleHandler = null;

                    idleHandler = async (s, e) =>
                    {
                        // handle Application.Idle just once
                        Application.Idle -= idleHandler;

                        // return to the message loop
                        await Task.Yield();

                        // and continue asynchronously
                        // propogate the result or exception
                        try
                        {
                            var result = await worker(args);
                            tcs.SetResult(result);
                        }
                        catch (Exception ex)
                        {
                            tcs.SetException(ex);
                        }

                        // signal to exit the message loop
                        // Application.Run will exit at this point
                        Application.ExitThread();
                    };

                    // handle Application.Idle just once
                    // to make sure we're inside the message loop
                    // and SynchronizationContext has been correctly installed
                    Application.Idle += idleHandler;
                    try
                    {
                        Application.Run();
                    }
                    catch (Exception)
                    {


                    }

                });

                // set STA model for the new thread
                thread.SetApartmentState(ApartmentState.STA);

                // start the thread and await for the task
                thread.Start();
                try
                {
                    return await tcs.Task;
                }


                finally
                {
                    thread.Join();
                }
            }
        }
    }





}

