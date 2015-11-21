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
using System.Data;

using System.Data.SqlClient;


namespace IndexQuotationService
{

    /// <summary>
    /// Класс отвечающий за отработку очереди 
    /// всех отложенных вызовов методов в объектах
    /// </summary>
    static class QuotationHeldCaller
    {
        // прототип делегата, который будет принимать методы
        public delegate void QuotationIndexDelegate();

        // массив делегатов, что будет принимать инстанцируемые объекты с их методами.
        // массив заполняется, ошибки здесь не было найдено.
        static Queue<QuotationIndexDelegate> HeldArray = new Queue<QuotationIndexDelegate>();

        // Отдельный поток, в котором будет весь массив очищатся 
        // сделано, что бы не грузить осн. поток и в случае чего, не крешить IIS
        static Thread MethodsQuery;

        // 5 минут - тестовое значение задержки. Нужно тестировать
        // при котором интервале будет google academy отвечать капчей
        static TimeSpan HeldTimeConst = new TimeSpan(0, 5, 0);

        static QuotationHeldCaller() { }

        /// <summary>
        /// Добавляет отложенный вызов, а именно:
        /// 1. MethodsQuery пуст или не существует (null) - создаст\запустит его
        /// 2. добавит в очередь делегатов HeldArray - QuotationIndexDelegate,  
        /// который в свою очередь приймет метод от инстанцируемого объекта QuotationObject
        /// Конструктор которого (в идеале) должен отработаться и уже хранить базовые данные прежде 
        /// чем попадет в отложенный вызов в цикле потока.
        /// </summary>
        /// <param name="LoginCookie">Cookie пользователя, его логин в системе</param>
        public static void AddHeld(string LoginCookie)
        {


            if (MethodsQuery != null)
                IsHeldArrayEmpty();

            if (MethodsQuery == null)
            {
                safelyStartThread();
            }
            else if (MethodsQuery != null && !MethodsQuery.IsAlive)
            {
                safelyStartThread();
            }

            // добавляем новый QuotationObject, с передачей ему параметра LoginCookie
            HeldArray.Enqueue(
                new QuotationIndexDelegate(
                        new QuotationObject(LoginCookie).QuotationIndexEvent
                    )
                );
        }

        /// <summary>
        /// Инстанцирование потока и запуск.
        /// </summary>
        static void safelyStartThread()
        {
            MethodsQuery = new Thread(new ThreadStart(Iterator));
            MethodsQuery.Start();
        }

        /// <summary>
        /// Итератор по интервалу с задержками указанной в HeldTimeConst
        /// вызывает по очереди каждый делегат из очереди делегатов, что указаны выше.
        /// TODO: понять что здесь происходит не так, и как вообще себя ведут await
        /// </summary>
        static void Iterator()
        {
            while (HeldArray.Count > 0)
            {
                Thread.Sleep(HeldTimeConst);
                DequeueFromDelegates();
            }
        }

        /// <summary>
        /// Вызываем метод из очереди.
        /// </summary>
        static void DequeueFromDelegates()
        {
            HeldArray.Dequeue().Invoke();
        }

        /// <summary>
        /// Проверить очередь на пустоту
        /// В случае если пусто - завершаем поток 
        /// и на всякий случай заново выделяем память на HeldArray;
        /// </summary>
        static void IsHeldArrayEmpty()
        {
            // проверить тред на отработку
            //если очередь пуста - остановить и очистить!!!
            //потоконебезопасно оставлять делегат в отработке, слишком много объектов висеть будет!

            if (HeldArray.Count == 0 && MethodsQuery.IsAlive)
            {
                MethodsQuery.Abort();

                // Во избежания объектов, что добавились в момент проверки этого условия.
                // Очищаем.
                HeldArray = new Queue<QuotationIndexDelegate>();
            }

        }
    }

    /// <summary>
    /// Объект, что хранит в себе базовые данные
    /// необходимые перед отработкой парсера индекса цитирования (отдельный класс)
    /// </summary>
    class QuotationObject
    {
        public string id;
        public string name;
        public string surname;
        public DataTable teacher;

        public QuotationObject(string LoginCookie)
        {

            this.teacher = DB.Query("Select Id, name, surname from Teachers where Login=@param0", new string[] { LoginCookie });

            this.id = teacher.Rows[0][0].ToString();
            this.name = teacher.Rows[0][1].ToString();
            this.surname = teacher.Rows[0][2].ToString();
        }

        /// <summary>
        /// Отработка основного парсера.
        /// </summary>
        public void QuotationIndexEvent()
        {
            string[] arrayres;


            var getter = new GoogleScholarDataGetter(string.Concat(surname, " ", name));
            // GoogleScholarDataGetter getter = new GoogleScholarDataGetter("Глазунова Олена");
            getter.executeParser();

            // возвращаемый массив
            arrayres = getter.OutputData;
            //if (arrayres == null)
            //    throw new ArgumentNullException("Парсер ничего не вернул!");

            // данные возвращаюся не прямым представлением, расстановка.
            string[] IndexesLibraryStr = new string[3] { arrayres[0], arrayres[3], arrayres[6] };
            string[] AllIndexesStr = new string[3] { arrayres[1], arrayres[4], arrayres[7] };
            string[] From2009Str = new string[3] { arrayres[2], arrayres[5], arrayres[8] };

            // вносим новые данные в бд
            string[] paramArray = { 
                                          id, 
                                          arrayres[1].ToString(), 
                                          arrayres[2].ToString(), 
                                          arrayres[4].ToString(), 
                                          arrayres[5].ToString(), 
                                          arrayres[7].ToString(), 
                                          arrayres[8].ToString() 
                                      };

            DB.Query("Insert into QuotationIndexs values (@param0,@param1,@param2,@param3,@param4,@param5,@param6)", paramArray);
        }


    }

    public static class DB
    {
        public static DataTable Query(string query, string[] parameters)
        {
            SqlConnection _con = new SqlConnection("Data Source=EPORTFOLIO;Initial Catalog=eportfolio;Integrated Security=True");
            SqlCommand cmd = _con.CreateCommand();
            cmd.Connection.Open();
            cmd.CommandType = System.Data.CommandType.Text;
            for (int i = 0; i < parameters.Length; i++)
            {
                cmd.Parameters.Add(new System.Data.SqlClient.SqlParameter("param" + i, parameters[i]));
            }
            cmd.CommandText = query;
            DataTable dt = new DataTable();
            dt.Load(cmd.ExecuteReader());

            _con.Close();
            if (dt != null)
                return dt;
            else
                return null;
        }
    }

}