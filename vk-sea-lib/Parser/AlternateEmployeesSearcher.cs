using Accord.MachineLearning.DecisionTrees;
using log4net;
using Morpher.Generic;
using Morpher.Russian;
using QuickGraph;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using vk_sea_lib.DecisionTreeBuild;
using vk_sea_lib.Parser.GraphOperations;
using vk_sea_lib.Resources;
using VkNet.Enums.Filters;
using VkNet.Enums.SafetyEnums;
using VkNet.Exception;
using VkNet.Model;
using VkNet.Model.Attachments;
using VkNet.Model.RequestParams;
using VkNet.Utils;


namespace vk_sea_lib
{
    class EmployeesSearcher
    {
        private static ILog logger = LogManager.GetLogger("EmployeesSearcher");
        public string companyName {
            get
            {
                return this.company_name;
            }
            set
            {
                this.company_name = value;
            }
        }
        public string vkPageId {
            get
            {
                return this.vk_company_page_id;
            }
            set
            {
                this.vk_company_page_id = value;
            }
        }

        // View parameter fields
        private string vk_company_page_id;
        private string company_name;
        private int count_affiliates;

        // Constructor
        public EmployeesSearcher(DecisionTreeBuilder decisionTree, string companyName, string vkPageId)
        {
            this.tree = decisionTree;

            this.companyName = companyName;
            this.vkPageId = vkPageId;
        }

        //queue to analyze and employee collection in graph:
        private DataTable dataset;
        private Dictionary<string, string> words_in_group;
        private Dictionary<long, int> likes_in_group;
        private DecisionTreeBuilder tree;


        //результирующий граф и список найденных сотрудинков
        public AdjacencyGraph<long, Edge<long>> EmployeesSocialGraph;
        public Dictionary<User, Boolean> EmployeesFoundList;
        public List<User> allFoundEmployees;

        //буфер для работы с частями
        public DataTable training_dataset;

        /**
         *  результат работы метода ---  EmployeesSocialGraph
         */
        public void findAllEmployees()
        {
            /**
             * init buffer dataset
             */
            this.training_dataset = new DataTable("decision tree trainer");

            this.training_dataset.Columns.Add("vk_id", typeof(long));

            this.training_dataset.Columns.Add("on_web", typeof(int));
            this.training_dataset.Columns.Add("has_firm_name", typeof(int));
            this.training_dataset.Columns.Add("likes_counter", typeof(int));
            this.training_dataset.Columns.Add("followed_by", typeof(int));
            this.training_dataset.Columns.Add("following_matches", typeof(int));
            this.training_dataset.Columns.Add("is_employee", typeof(int));

            this.training_dataset.Columns.Add("first_name", typeof(string));
            this.training_dataset.Columns.Add("last_name", typeof(string));
            

            /**
              *  Собираем пользователей, c has_firm_name = true
              */
            List<User> has_firm_name_employees = VkApiHolder.Api.Users.Search(new UserSearchParams
            {
                Company = this.company_name,
                Count = 1000

            }).ToList();

            /**
             * 
             *  Собираем посты официальной группы
             * 
             */
            List<Post> group_posts = new List<Post>();
            List<Photo> group_photos = new List<Photo>();

            makeLikesDictionary(group_posts, group_photos);

            try
            {
                group_posts = VkApiHolder.Api.Wall.Get(new WallGetParams()
                {
                    OwnerId = Convert.ToInt32("-" + vk_company_page_id),
                    Count = 100,
                    Filter = WallFilter.Owner
                }).WallPosts.ToList();


                group_photos = VkApiHolder.Api.Photo.Get(new PhotoGetParams()
                {
                    OwnerId = Convert.ToInt32("-" + vk_company_page_id),
                    Count = 1000,
                    Extended = true,
                    AlbumId = PhotoAlbumType.Profile
                }).ToList();
            }
            catch (AccessDeniedException ex)
            {
                logger.Error("Access Denied Exception: " + ex.Message);
                logger.Error("_______________________________________");
            }

            makeDictionary(group_posts);


            //insert dataset into datatable
            /**
             *    DataRow Format: 
             *      
             *      row[0] = vk_id
             *      
             *      row[1] = on_web
             *      row[2] = has_firm_name
             *      row[3] = likes_counter
             *      row[4] = followed_by
             *      row[5] = following_matches
             *      row[6] = is_employee
             *    
             *      row[7] = first_name
             *      row[8] = last_name
             *    
             */

            EmployeeSearcher blackEmployeeStatusSetter = new EmployeeSearcher(has_firm_name_employees, tree, training_dataset, group_posts, group_photos, 1000, this.vk_company_page_id, this.words_in_group);
            blackEmployeeStatusSetter.initialize_searcher();

            foreach (KeyValuePair<long, string> black_vertice in blackEmployeeStatusSetter.getAllBlackStatusedEmp)
            {
                if (black_vertice.Value.Equals("black"))
                {
                    logger.Info("RESULT OF RESEARCH: FOUND EMPLOYEE " + black_vertice.Key);
                }
            }

            //сохраняем в граф всех найденных сотрудников
            //fillEmployeesIntoGraph();
        }

        /**
         * метод отсеивает ранее проанализированные страницы
         * и сохраняет результаты работы дерева в EmployeesFoundList
         */

        /**
         *   TODO: переделать в рекурсивный вызов, опробовать концепции 1st level с рекурсией или 2nd level 
         */  
        private void collectFriendsEmployees(User employee, List<Post> group_posts, List<Photo> group_photos)
        {

            List<User> affiliate_friends = new List<User>();

            try
            {
                /**
                 *  TODO: тут ограничили функционал
                 */ 
                Thread.Sleep(100);
                affiliate_friends = VkApiHolder.Api.Friends.Get(new FriendsGetParams
                {
                    UserId = Convert.ToInt32(employee.Id),
                    Order = FriendsOrder.Hints,
                    //    Count = 100,
                    Fields = (ProfileFields)(ProfileFields.Domain)

                }).ToList<User>();

                //datasetfriends.Add(employee, affiliate_friends);
            }
            catch (TooManyRequestsException ex)
            {
                Thread.Sleep(300);
                logger.Error("Too many requests exception");
            }

            if (affiliate_friends.Count != 0)
            {
                foreach (User affiliate in affiliate_friends)
                {
                    if (EmployeesFoundList.ContainsKey(affiliate))
                    {
                        affiliate_friends.Remove(affiliate);
                    }
                }

                //остались только новые страницы, данных пользователей ранее не встречали:

                /**
                 * инициализируем таблицу для заполнения найденных параметров
                 */
                this.training_dataset.Rows.Clear();

                foreach (User training_employee in affiliate_friends)
                {
                    DataRow row = this.training_dataset.NewRow();

                    row[0] = training_employee.Id;

                    row[1] = 0;
                    row[2] = 0;
                    row[3] = 0;
                    row[4] = 0;
                    row[5] = 0;
                    row[6] = 0;

                    row[7] = training_employee.FirstName;
                    row[8] = training_employee.LastName;

                    training_dataset.Rows.Add(row);
                }

                analyzeBufferParams(affiliate_friends, group_posts, group_photos);

                /**
                 *  проходим по дереву для проанализированных страниц
                 */

                 /**
                  * TODO: вот тут какая-то пародия на garbage collector
                  */  
                DataTable symbols = tree.codebook.Apply(training_dataset);
                foreach (DataRow row in symbols.Rows)
                {
                    double r1 = Convert.ToDouble(row[1]);
                    double r3 = Convert.ToDouble(row[3]);
                    double r4 = Convert.ToDouble(row[4]);
                    double r5 = Convert.ToDouble(row[5]);

                    int is_employee = this.tree.func(new double[] { r1, r3, r4, r5 });

                    if (is_employee == 0)
                    {
                        try
                        {
                            Thread.Sleep(100);

                            EmployeesFoundList.Add(VkApiHolder.Api.Users.Get((long)row[0], ProfileFields.LastName), false);
                            logger.Debug("not employee"+ VkApiHolder.Api.Users.Get((long)row[0], ProfileFields.LastName).ToString());
                        }
                        catch (TooManyRequestsException req_ex)
                        {
                            Thread.Sleep(100);
                            EmployeesFoundList.Add(VkApiHolder.Api.Users.Get((long)row[0], ProfileFields.LastName), false);
                        }

                    }
                    else if (is_employee == 1)
                    {
                        try
                        {
                            Thread.Sleep(100);

                            EmployeesFoundList.Add(VkApiHolder.Api.Users.Get((long)row[0], ProfileFields.LastName), true);
                            Console.WriteLine("_____ сотрудник!!!!");
                        }
                        catch (TooManyRequestsException req_ex)
                        {
                            Thread.Sleep(100);
                            EmployeesFoundList.Add(VkApiHolder.Api.Users.Get((long)row[0], ProfileFields.LastName), false);
                        }

                    }
                }
            }
        }

        /**
         * анализируем параметры для буфера
         */
        private void analyzeBufferParams(List<User> affiliates, List<Post> group_posts, List<Photo> group_photos)
        {
            // заполняем onWeb параметр
            searchInGroupPosts(affiliates);

            // заполняем likesCounter параметр
            searchInGroupLikes(group_posts, group_photos);

            // анализируем топологию сети для буфера
            analyzeNetworkTopology(affiliates);
        }


        /**
        *  _________________________________________
        *  Метод заполнения графа связей сотрудников
        *  _________________________________________
        *  
        */

        private void fillEmployeesIntoGraph()
        {
            // инициализируем граф друзей для сотрудников
            this.EmployeesSocialGraph = new AdjacencyGraph<long, Edge<long>>();

            /**
             * 
             * 1.1) удаляем лишние поля
             * 1.2) добавляем сотрудников в граф
             * 
             * 2.1) добавляем связи в граф 
             * 
             */

            this.allFoundEmployees = new List<User>();
            foreach (KeyValuePair<User, Boolean> decision_about_user in EmployeesFoundList)
            {
                if (decision_about_user.Value)
                {
                    allFoundEmployees.Add(decision_about_user.Key);
                }
            }
            foreach (User emp in allFoundEmployees)
            {
                this.EmployeesSocialGraph.AddVertex(emp.Id);
            }


            foreach (User decision_about_user in allFoundEmployees)
            {

                /**
                 *  получаем список друзей выявленного сотрудника
                 */

                List<User> vertexFriends = new List<User>();

                try
                {
                    Thread.Sleep(100);
                    vertexFriends = VkApiHolder.Api.Friends.Get(new FriendsGetParams
                    {
                        UserId = Convert.ToInt32(decision_about_user.Id),
                        Order = FriendsOrder.Hints,
                        Count = 100,
                        Fields = (ProfileFields)(ProfileFields.Domain)

                    }).ToList<User>();
                }
                catch (TooManyRequestsException ex)
                {
                    Thread.Sleep(300);
                }

                foreach (User vertexfriend in vertexFriends)
                {
                    if (this.EmployeesSocialGraph.ContainsVertex(vertexfriend.Id))
                    {
                        try
                        {
                            EmployeesSocialGraph.AddEdge(new Edge<long>(vertexfriend.Id, decision_about_user.Id));
                        }
                        catch (Exception ex)
                        {

                        }
                    }
                }
            }
        }

        /**
         *  ________________________________
         *  Методы анализа параметров буфера
         *  ________________________________
         *  
         */
        private void makeLikesDictionary(List<Post> group_posts, List<Photo> group_photos)
        {
            this.likes_in_group = new Dictionary<long, int>();

            // считаем лайки к постам группы
            foreach (var post in group_posts)
            {
                VkCollection<long> likes = VkApiHolder.Api.Likes.GetList(new LikesGetListParams
                {
                    Type = LikeObjectType.Post,
                    OwnerId = post.OwnerId,
                    ItemId = (long)post.Id

                });

                foreach (long user_likes_post in likes)
                {
                    if (likes_in_group.Keys.Contains(user_likes_post)) likes_in_group[user_likes_post]++;
                    else likes_in_group.Add(user_likes_post, 1);
                }

                Thread.Sleep(100);
            }

            // считаем лайки к фотографиям
            foreach (var photo in group_photos)
            {
                VkCollection<long> likes = VkApiHolder.Api.Likes.GetList(new LikesGetListParams
                {
                    Type = LikeObjectType.Post,
                    OwnerId = photo.OwnerId,
                    ItemId = (long)photo.Id

                });

                foreach (long user_likes_post in likes)
                {
                    if (likes_in_group.Keys.Contains(user_likes_post)) likes_in_group[user_likes_post]++;
                    else likes_in_group.Add(user_likes_post, 1);
                }
            }
        }
        private void makeDictionary(List<Post> group_posts)
        {
            this.words_in_group = new Dictionary<string, string>();
            foreach (Post group_post in group_posts)
            {
                string post_txt = group_post.Text.ToLower();
                string[] words_in_post = GetWords(post_txt);

                foreach (string word in words_in_post)
                {
                    if (!this.words_in_group.ContainsKey(word))
                        this.words_in_group.Add(word, word);
                }
            }

            logger.Debug("collected all group posts");
            logger.Debug("total number of words: " + words_in_group.Count());
        }

        private void analyzeNetworkTopology(List<User> affiliates)
        {
            Dictionary<User, List<User>> datasetfriends = new Dictionary<User, List<User>>();


            // собираем друзей пользователя
            foreach (User user in affiliates)
            {
                try
                {
                    Thread.Sleep(100);
                    var affiliate_friends = VkApiHolder.Api.Friends.Get(new FriendsGetParams
                    {
                        UserId = Convert.ToInt32(user.Id),
                        Order = FriendsOrder.Hints,
                        Fields = (ProfileFields)(ProfileFields.Domain)

                    }).ToList<User>();
                    datasetfriends.Add(user, affiliate_friends);
                }
                catch (TooManyRequestsException ex)
                {
                    Thread.Sleep(300);
                }
                catch (VkApiException e)
                {
                    Thread.Sleep(300);
                }

            }


            int totalCount;
            var followers = VkApiHolder.Api.Groups.GetMembers(out totalCount, new GroupsGetMembersParams
            {
                GroupId = this.vk_company_page_id
            }).ToList<User>();


            /**
             *  matchesFound --- 
             *      key -> User
             *      val -> список его друзей состоящих в группе
             */

            var matchesFound = searchFollowingMatches(followers, datasetfriends);

            string filterExpression, sortOrder;
            foreach (KeyValuePair<long, List<int>> user_to_update_id in matchesFound)
            {
                filterExpression = "vk_id = '" + user_to_update_id.Key + "'";
                sortOrder = "vk_id DESC";
                DataRow[] users_found_surname = training_dataset.Select(filterExpression, sortOrder, DataViewRowState.Added);

                foreach (DataRow row in users_found_surname)
                {
                    row[5] = (user_to_update_id.Value.Count - 1);
                }
            }
        }

        private void searchInGroupPosts(List<User> affiliates)
        {

            System.Net.ServicePointManager.Expect100Continue = false;
           // IDeclension declension = Morpher.Factory.Russian.Declension;

            string filterExpression;
            string sortOrder;

            foreach (User affiliate in affiliates)
            {
                bool match_found = false;

                List<string> surname_diclensions = makeSurnameValuesToSearch(affiliate.LastName);
                foreach (string surname_in_dimension in surname_diclensions)
                {
                    if (words_in_group.ContainsValue(surname_in_dimension)) match_found = true;
                }


                if (match_found)
                {
                    filterExpression = "vk_id = '" + affiliate.Id + "'";
                    sortOrder = "vk_id DESC";
                    DataRow[] users_found_surname = training_dataset.Select(filterExpression, sortOrder, DataViewRowState.Added);

                    foreach (DataRow row in users_found_surname)
                    {
                        row[1] = 1;
                    }

                }
            }
        }

        private List<string> makeSurnameValuesToSearch(string surname)
        {
            surname = surname.ToLower();
            List<string> surname_declensions = new List<string>();

            try
            {
                  Morpher.Russian.IDeclension declension = Morpher.Factory.Russian.Declension;

                  surname_declensions.Add(declension.Parse(surname).Nominative);
                  surname_declensions.Add(declension.Parse(surname).Genitive);
                  surname_declensions.Add(declension.Parse(surname).Dative);
                  surname_declensions.Add(declension.Parse(surname).Accusative);
                  surname_declensions.Add(declension.Parse(surname).Instrumental);
                  surname_declensions.Add(declension.Parse(surname).Prepositional); 

                surname_declensions.Add(surname);
            }
            catch (Exception ex)
            {
                surname_declensions.Add(surname);
            }
            return surname_declensions;
        }

        static string[] GetWords(string input)
        {
            MatchCollection matches = Regex.Matches(input, @"\b[\w']*\b");

            var words = from m in matches.Cast<Match>()
                        where !string.IsNullOrEmpty(m.Value)
                        select TrimSuffix(m.Value);

            return words.ToArray();
        }
        static string TrimSuffix(string word)
        {
            int apostropheLocation = word.IndexOf('\'');
            if (apostropheLocation != -1)
            {
                word = word.Substring(0, apostropheLocation);
            }

            return word;
        }

        private void searchInGroupLikes(List<Post> group_posts, List<Photo> group_photos)
        {
            string filterExpression, sortOrder;

            foreach (KeyValuePair<long, int> likes_by_user in this.likes_in_group)
            {
                filterExpression = "vk_id = '" + likes_by_user.Key + "'";
                sortOrder = "vk_id DESC";
                DataRow[] users_found_surname = training_dataset.Select(filterExpression, sortOrder, DataViewRowState.Added);

                foreach (DataRow row in users_found_surname)
                {
                    row[3] = likes_by_user.Value;
                    logger.Debug("liked " + row[3] + " posts by affiliate " + row[0]);

                }
            }
        }
        private void searchInGroupLikes(List<Post> group_posts)
        {
            Dictionary<long, int> likes_id = new Dictionary<long, int>();

            foreach (var post in group_posts)
            {
                VkCollection<long> likes = VkApiHolder.Api.Likes.GetList(new LikesGetListParams
                {
                    Type = LikeObjectType.Post,
                    OwnerId = post.OwnerId,
                    ItemId = (long)post.Id

                });

                foreach (long user_likes_post in likes)
                {
                    if (likes_id.Keys.Contains(user_likes_post)) likes_id[user_likes_post]++;
                    else likes_id.Add(user_likes_post, 1);
                }

            }


        }


        private Dictionary<long, List<int>> searchFollowingMatches(List<int> group_followers_ids, Dictionary<long, List<int>> dataset_ids)
        {
            Dictionary<long, List<int>> rez = new Dictionary<long, List<int>>();

            group_followers_ids.Sort();
            foreach (KeyValuePair<long, List<int>> entry in dataset_ids)
            {
                entry.Value.Sort();

                rez.Add(entry.Key, GetSimilarID(entry.Value, group_followers_ids));
                logger.Debug("for affiliate" + entry.Key + " found " + entry.Value.Count());
            
                // TODO: CHECK STRING BELOW
                // GetSimilarID(entry.Value, group_followers_ids).ForEach(i => Console.Write("{0}\t", i));
            }
            return rez;
        }
        private Dictionary<long, List<int>> searchFollowingMatches(List<User> group_followers, Dictionary<User, List<User>> dataset)
        {
            List<int> followers_ids = new List<int>();
            Dictionary<long, List<int>> dataset_ids = new Dictionary<long, List<int>>();

            foreach (User user in group_followers)
            {
                followers_ids.Add((int)user.Id);
            }
            foreach (KeyValuePair<User, List<User>> entry in dataset)
            {
                List<int> _friends = new List<int>();
                foreach (User user in entry.Value)
                {
                    _friends.Add((int)user.Id);
                }
                try
                {
                    dataset_ids.Add(entry.Key.Id, _friends);
                }
                catch (Exception ex)
                {
                    logger.Error("exception occured during network topology analyze");
                    logger.Error(ex.Message);
                }
            }

            return searchFollowingMatches(followers_ids, dataset_ids);
        }

        private List<int> GetSimilarID(IEnumerable<int> list1, IEnumerable<int> list2)
        {
            return (from item in list1 from item2 in list2 where (item == item2) select item).ToList();
        }
    }
}
