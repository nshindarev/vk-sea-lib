using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace vk_sea_lib.Authorize
{
    public class UserAuthorizer
    {
        /**
         *  waiter: ждет заполнения логина и пароля 
         */
        public static AutoResetEvent browserWaiter = new AutoResetEvent(false);
        /**
         * сохраняем тут результаты работы в браузере
         */
        public static string access_token { get; set; }
        public static int user_id { get; set; }
        /**
         * Поля для авторизации пользователя в приложении 
         */
        public static string api_url = "https://api.vk.com/";
        public static int app_id = 5677623;

        private enum VkontakteScopeList
        {
            notify = 1,
            friends = 2,
            photos = 4,
            audio = 8,
            video = 16,
            offers = 32,
            questions = 64,
            pages = 128,
            link = 256,
            notes = 2048,
            messages = 4096,
            wall = 8192,
            docs = 131072,
            groups = 262144
        }

        public static int scope = (int)(
            VkontakteScopeList.friends |
            VkontakteScopeList.groups |
            VkontakteScopeList.wall);

        /// <summary>
        /// метод запускает IE и сохраняет access_token и user_id
        /// </summary>
        public void authorize()
        {
            UserAuthorizer.access_token = "";
            UserAuthorizer.user_id = 0;


            Thread t = new Thread(browserThread);
            t.Name = "browserThread";
            t.Start();

            Console.ReadLine();
        }
        private void browserThread()
        {
            EventHandlers e = new EventHandlers();
            SHDocVw.InternetExplorer IE = new SHDocVw.InternetExplorer();

            object Empty = 0;
            object URL = String.Format("https://api.vk.com/oauth/authorize?client_id={0}&scope={1}&display=popup&response_type=token", app_id, scope);

            // override Internet Explorer events
            IE.DocumentComplete += e.OnLogPassInserted;

            IE.Visible = true;
            IE.Navigate2(ref URL, ref Empty, ref Empty, ref Empty, ref Empty);

            browserWaiter.WaitOne();
            IE.Quit();


        }
        private class EventHandlers
        {
            public void OnLogPassInserted(object sender, ref object Url)
            {
                if (Url.ToString().IndexOf("access_token") != -1)
                {
                    UserAuthorizer.access_token = "";
                    UserAuthorizer.user_id = 0;

                    Regex myReg = new Regex(@"(?<name>[\w\d\x5f]+)=(?<value>[^\x26\s]+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    foreach (Match m in myReg.Matches(Url.ToString()))
                    {
                        if (m.Groups["name"].Value == "access_token")
                        {
                            UserAuthorizer.access_token = m.Groups["value"].Value;
                        }
                        else if (m.Groups["name"].Value == "user_id")
                        {
                            UserAuthorizer.user_id = Convert.ToInt32(m.Groups["value"].Value);
                        }
                        // еще можно запомнить срок жизни access_token - expires_in,
                        // если нужно
                    }
                }

                Console.WriteLine(UserAuthorizer.access_token);
                Console.WriteLine(UserAuthorizer.user_id.ToString());



                if (!(UserAuthorizer.access_token == "") && !(UserAuthorizer.user_id == 0))
                    UserAuthorizer.browserWaiter.Set();
            }
        }
    }
}
