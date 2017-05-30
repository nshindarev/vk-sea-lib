using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VkNet;


namespace vk_sea_lib.Resources
{
    public class VkApiHolder
    {
        private static readonly Lazy<VkApi> vkApiHolder = new Lazy<VkApi>(() => new VkApi());

        private VkApiHolder() { }

        public static VkApi Api {
            get { return vkApiHolder.Value; }
        }
    }
}
