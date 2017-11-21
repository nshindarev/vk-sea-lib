using System;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vk_sea_lib;
using vk_sea_lib.Main;
using vk_sea_lib.Authorize;

namespace connector_simulator
{
    class Program
    {
        static void Main(string[] args)
        {
            UserAuthorizer auth = new vk_sea_lib.Authorize.UserAuthorizer();
            auth.authorize();

            CreateSocialGraph creator = new CreateSocialGraph(UserAuthorizer.access_token, UserAuthorizer.user_id.ToString());
            creator.createSocialGraph();

        }
    }
}
