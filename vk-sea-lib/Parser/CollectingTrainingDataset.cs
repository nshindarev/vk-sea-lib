﻿using log4net;
using Morpher.Generic;
using Morpher.Russian;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using vk_sea_lib.Authorize;
using vk_sea_lib.Resources;
using VkNet.Enums.Filters;
using VkNet.Enums.SafetyEnums;
using VkNet.Exception;
using VkNet.Model;
using VkNet.Model.Attachments;
using VkNet.Model.RequestParams;
using VkNet.Utils;


namespace vk_sea_lib.Parser
{
    class CollectingTrainingDataset
    {
        private static ILog logger = LogManager.GetLogger("CollectingTrainingDataset");

        public CollectingTrainingDataset(string companyName, string vkPageId)
        {
            this.companyName = companyName;
            this.vkPageId = vkPageId;

            logger.Debug("start of collecting training dataset for classifier");
            logger.Debug("COMPANY: " + companyName);
            logger.Debug("VK ID:   " + vkPageId);
            logger.Debug("___________________________________________________");    
        }
        // api fields
        private static string api_url = "https://api.vk.com/";
        private static int app_id = 5677623;
        private string version = "5.60";


        // api parse config fields
        private uint search_employees_count = 1000;
        private uint count_per_user = 20;
        private uint max_count = 600;
        private int count_affiliates;

        // View parameter fields
        private string vk_company_page_id;
        private string company_name;

        //train and test dataset
        public DataTable training_dataset;
        private Dictionary<string, string> words_in_group;
        private Dictionary<long, int> likes_in_group;

        public enum VkontakteScopeList
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
            docs = 131072
        }

        public static int scope = (int)(VkontakteScopeList.audio |
            VkontakteScopeList.docs |
            VkontakteScopeList.friends |
            VkontakteScopeList.link |
            VkontakteScopeList.messages |
            VkontakteScopeList.notes |
            VkontakteScopeList.notify |
            VkontakteScopeList.offers |
            VkontakteScopeList.pages |
            VkontakteScopeList.photos |
            VkontakteScopeList.questions |
            VkontakteScopeList.video |
            VkontakteScopeList.wall);

        public void parseInformation()
        {
            //Init columns in dataset
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

            VkApiHolder.Api.Authorize(UserAuthorizer.access_token);

            // collect users with hasFirmName param
            List<User> has_firm_name_employees = VkApiHolder.Api.Users.Search(new UserSearchParams
            {
                Company = this.company_name,
                Count = 1000

            }).ToList();
            logger.Debug("Found " + has_firm_name_employees.Count() + " employees with has_firm_name == true");
            logger.Debug("_________________________________________________________________________________");


            this.count_affiliates = 4 * has_firm_name_employees.Count();

            // try to collect official group posts and photos
            List<Post> group_posts = new List<Post>();
            List<Photo> group_photos = new List<Photo>();

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

            List<User> has_another_firm_name = new List<User>();
            foreach (User employee in has_firm_name_employees)
            {
                if (has_another_firm_name.Count() <= this.count_affiliates)
                {

                    List<User> employee_friends = VkApiHolder.Api.Friends.Get(new FriendsGetParams
                    {
                        UserId = Convert.ToInt32(employee.Id.ToString()),
                        Order = FriendsOrder.Hints,
                        Fields = (ProfileFields)(ProfileFields.FirstName |
                                                 ProfileFields.LastName |
                                                 ProfileFields.Career)

                    }).ToList<User>();
                    Thread.Sleep(100);

                    
                    /**
                     *  Позволяет выявить друзей уже найденных сотрудников, которые работают в другой компании
                     */ 
                    foreach (User employee_friend in employee_friends)
                    {
                        bool match_found = false;
                        for (int i = 0; i < employee_friend.Career.Count; i++)
                        {
                            BoyerMoore search_by_name = new BoyerMoore(this.company_name);
                            BoyerMoore search_by_id = new BoyerMoore(this.vkPageId.ToString());


                            if (employee_friend.Career[i].Company != null)
                            {
                                if (search_by_name.Search((employee_friend.Career[i].Company)) != -1)
                                {
                                    match_found = true;
                                    //Console.WriteLine("match found: user id = {0}", employee_friend.LastName);
                                }
                            }
                            else
                            {
                                if (search_by_id.Search((employee_friend.Career[i].GroupId.ToString())) != -1)
                                {
                                    match_found = true;
                                }
                            }
                        }

                        if (!match_found && employee_friend.Career.Count != 0)
                        {
                            has_another_firm_name.Add(employee_friend);
                            if (has_another_firm_name.Count() % 100 == 0)
                                logger.Debug("Found 100 new non-employees, non-employees in training dataset == "+ has_another_firm_name.Count());
                        }
                    }
                }

            }

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

            logger.Info(".....................................");
            logger.Info("Finished collecting training dataset.");
            logger.Info("Found non-employees: " + has_another_firm_name.Count());
            logger.Info("Found employees:     " + has_firm_name_employees.Count());
            logger.Info("........................");

            foreach (User training_employee in has_firm_name_employees)
            {
                DataRow row = this.training_dataset.NewRow();

                row[0] = training_employee.Id;

                row[1] = 0;
                row[2] = 1;
                row[3] = 0;
                row[4] = 0;
                row[5] = 0;
                row[6] = 1;

                row[7] = training_employee.FirstName;
                row[8] = training_employee.LastName;

                training_dataset.Rows.Add(row);
            }

            foreach (User training_employee in has_another_firm_name)
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

            makeDictionary(group_posts);
            searchInGroupPosts(has_firm_name_employees);
            searchInGroupPosts(has_another_firm_name);

            searchInGroupLikes(group_posts, group_photos);

            #region ANALYSE TOPOLOGY
            Dictionary<User, List<User>> datasetfriends = new Dictionary<User, List<User>>();


            foreach (User user in has_firm_name_employees)
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
                    logger.Warn("VK too many requests exception");

                }

            }



            int totalCount;
            var followers = VkApiHolder.Api.Groups.GetMembers(out totalCount, new GroupsGetMembersParams
            {
                GroupId = this.vk_company_page_id
            }).ToList<User>();

            var matchesFound = searchFollowingMatches(followers, datasetfriends);
            #endregion

            string filterExpression, sortOrder;
            foreach (KeyValuePair<long, List<int>> user_to_update_id in matchesFound)
            {
                filterExpression = "vk_id = '" + user_to_update_id.Key + "'";
                sortOrder = "vk_id DESC";
                DataRow[] users_found_surname = training_dataset.Select(filterExpression, sortOrder, DataViewRowState.Added);

                foreach (DataRow row in users_found_surname)
                {
                    row[5] = user_to_update_id.Value.Count;
                }
            }
        }

        /// <summary>
        /// метод ищет упоминание фамилии сотрудника в группе
        /// </summary>
        /// <param name="group_wall_data"></param>
        /// <param name="affiliates"></param>
        private void searchInGroupPosts(List<User> affiliates)
        {

            string filterExpression;
            string sortOrder;

            System.Net.ServicePointManager.Expect100Continue = false;

            try
            {
                //IDeclension declension = Morpher.Factory.Russian.Declension;
            }
            catch(TypeInitializationException morpher_ex)
            {
                logger.Error(morpher_ex.Message);
            }

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
                        logger.Debug("affiliate " + row[0] + " was mentioned in group");
                    }

                }
            }

        }

        /**
         *  TODO: на данном этапе должен осуществляться препроцессинг текста
         */ 
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
            makeLikesDictionary(group_posts, group_photos);


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

        private void makeLikesDictionary(List<Post> group_posts, List<Photo> group_photos)
        {
            this.likes_in_group = new Dictionary<long, int>();

            // считаем лайки к постам группы
            foreach (var post in group_posts)
            {
                try
                {
                    Thread.Sleep(100);

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
                catch (TooManyRequestsException ex)
                {
                    Thread.Sleep(200);
                    logger.Warn("VK too many requests exception");
                }


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


        /// <summary>
        /// анализ топологии сети
        /// </summary>
        /// <param name="dataset_ids"> id всех сотрудников из БД </param>
        /// <param name="group_followers_ids"> id подписчиков официальной группы </param>
        private Dictionary<long, List<int>> searchFollowingMatches(List<int> group_followers_ids, Dictionary<long, List<int>> dataset_ids)
        {
            Dictionary<long, List<int>> rez = new Dictionary<long, List<int>>();

            group_followers_ids.Sort();
            foreach (KeyValuePair<long, List<int>> entry in dataset_ids)
            {
                entry.Value.Sort();

                rez.Add(entry.Key, GetSimilarID(entry.Value, group_followers_ids));
                logger.Debug("for affiliate"+ entry.Key + " found"+ entry.Value.Count());
               
                //TODO : CHECK STRING BELOW
                //GetSimilarID(entry.Value, group_followers_ids).ForEach(i => Console.Write("{0}\t", i));
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

        //Interface getter/setter
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
    }
}

