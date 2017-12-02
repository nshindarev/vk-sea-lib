using System;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vk_sea_lib;
using vk_sea_lib.Main;
using vk_sea_lib.Authorize;
using vk_sea_lib.Resources;
using log4net.Config;
using log4net;

namespace connector_simulator
{
    class Program
    {
        private static ILog logger = LogManager.GetLogger("Program");
        static void Main(string[] args)
        {
            /*необходимо для предварительной конфигурации логгера*/
            XmlConfigurator.Configure();
            logger.Info("logger configured succsessfully");

            UserAuthorizer auth = new vk_sea_lib.Authorize.UserAuthorizer();
            auth.authorize();

            CreateSocialGraph creator = new CreateSocialGraph(UserAuthorizer.access_token, UserAuthorizer.user_id.ToString());
            creator.createSocialGraph();

        }
    }
}
