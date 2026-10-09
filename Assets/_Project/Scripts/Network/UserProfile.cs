using System;
using UnityEngine;

namespace SmartSpace.Network
{
    [Serializable]
    public class UserProfile
    {
        public string username = "星际漫游者";
        public int avatarId = 0; // 0 ~ 5
        public string gender = "secret"; // "male" | "female" | "secret"
        public int age = 0; // 0 表示保密/未填, 1~120
        public string bio = "探索智慧空间，结交好友！";
        public int level = 100;
        public string uid = "9474911";
        public string region = "中国湖北";
        public string guild = "无";
        public string title = "天赋异禀";
        public string birthday = "2002-05-20";
        public string status = "休闲中 ☕";
        public int flowers = 4080;
        public int achievePoints = 3440;
        public int residenceDays = 365;
        public int friendsCount = 8;
        public int costumeId = 0;

        public static readonly string[] StatusPresets = new string[]
        {
            "休闲中 ☕",
            "放松中 🍃",
            "漫游中 🚀",
            "发呆中 ☁️",
            "探险中 🗺️",
            "忙碌中 ⚡"
        };

        private const string PREF_KEY_HAS_PROFILE = "SmartSpace_HasProfile";
        private const string PREF_KEY_USERNAME = "SmartSpace_Profile_Username";
        private const string PREF_KEY_AVATAR = "SmartSpace_Profile_Avatar";
        private const string PREF_KEY_GENDER = "SmartSpace_Profile_Gender";
        private const string PREF_KEY_AGE = "SmartSpace_Profile_Age";
        private const string PREF_KEY_BIO = "SmartSpace_Profile_Bio";
        private const string PREF_KEY_LEVEL = "SmartSpace_Profile_Level";
        private const string PREF_KEY_UID = "SmartSpace_Profile_UID";
        private const string PREF_KEY_REGION = "SmartSpace_Profile_Region";
        private const string PREF_KEY_GUILD = "SmartSpace_Profile_Guild";
        private const string PREF_KEY_TITLE = "SmartSpace_Profile_Title";
        private const string PREF_KEY_FLOWERS = "SmartSpace_Profile_Flowers";
        private const string PREF_KEY_BIRTHDAY = "SmartSpace_Profile_Birthday";
        private const string PREF_KEY_STATUS = "SmartSpace_Profile_Status";
        private const string PREF_KEY_COSTUME = "SmartSpace_Profile_Costume";

        public static readonly string[] AvatarNames = new string[]
        {
            "蔚蓝旅人",
            "烈焰神宠",
            "萌狐精灵",
            "森之守护",
            "幻夜星辰",
            "极地雪灵"
        };

        public static readonly Color[] AvatarPrimaryColors = new Color[]
        {
            new Color(0.0f, 0.65f, 0.95f),  // Sky Blue
            new Color(0.95f, 0.25f, 0.25f), // Crimson Flame
            new Color(1.0f, 0.65f, 0.15f),  // Fox Orange
            new Color(0.20f, 0.85f, 0.45f), // Emerald Forest
            new Color(0.70f, 0.35f, 0.95f), // Nebula Purple
            new Color(0.25f, 0.85f, 0.95f)  // Glacial Cyan
        };

        public static readonly Color[] AvatarSecondaryColors = new Color[]
        {
            new Color(0.40f, 0.85f, 1.0f),
            new Color(1.0f, 0.55f, 0.35f),
            new Color(1.0f, 0.85f, 0.35f),
            new Color(0.55f, 0.95f, 0.70f),
            new Color(0.90f, 0.65f, 1.0f),
            new Color(0.75f, 0.95f, 1.0f)
        };

        public static bool HasSavedProfile()
        {
            return PlayerPrefs.GetInt(PREF_KEY_HAS_PROFILE, 0) == 1;
        }

        public static UserProfile LoadFromPrefs()
        {
            var p = new UserProfile();
            if (HasSavedProfile())
            {
                p.username = PlayerPrefs.GetString(PREF_KEY_USERNAME, GenerateRandomNickname());
                p.avatarId = Mathf.Clamp(PlayerPrefs.GetInt(PREF_KEY_AVATAR, 0), 0, AvatarNames.Length - 1);
                p.gender = PlayerPrefs.GetString(PREF_KEY_GENDER, "secret");
                p.age = PlayerPrefs.GetInt(PREF_KEY_AGE, 0);
                p.bio = PlayerPrefs.GetString(PREF_KEY_BIO, "这家伙太懒,什么都没留下...");
                p.level = PlayerPrefs.GetInt(PREF_KEY_LEVEL, 100);
                p.uid = PlayerPrefs.GetString(PREF_KEY_UID, "9474911");
                p.region = PlayerPrefs.GetString(PREF_KEY_REGION, "中国湖北");
                p.guild = PlayerPrefs.GetString(PREF_KEY_GUILD, "无");
                p.title = PlayerPrefs.GetString(PREF_KEY_TITLE, "天赋异禀");
                p.flowers = PlayerPrefs.GetInt(PREF_KEY_FLOWERS, 4080);
                p.birthday = PlayerPrefs.GetString(PREF_KEY_BIRTHDAY, "2002-05-20");
                if (!string.IsNullOrEmpty(p.birthday) && p.birthday != "保密" && p.birthday.Length <= 5)
                {
                    p.birthday = "2002-" + p.birthday;
                }
                p.status = PlayerPrefs.GetString(PREF_KEY_STATUS, "休闲中 ☕");
                p.costumeId = PlayerPrefs.GetInt(PREF_KEY_COSTUME, 0);
            }
            else
            {
                p.username = GenerateRandomNickname();
                p.avatarId = UnityEngine.Random.Range(0, AvatarNames.Length);
                p.gender = "secret";
                p.age = 0;
                p.bio = "这家伙太懒,什么都没留下...";
                p.level = 100;
                p.uid = UnityEngine.Random.Range(1000000, 9999999).ToString();
                p.region = "中国湖北";
                p.guild = "无";
                p.title = "天赋异禀";
                p.birthday = "2002-05-20";
                p.status = "休闲中 ☕";
                p.flowers = 4080;
                p.costumeId = 0;
            }
            return p;
        }

        public void SaveToPrefs()
        {
            PlayerPrefs.SetInt(PREF_KEY_HAS_PROFILE, 1);
            PlayerPrefs.SetString(PREF_KEY_USERNAME, username);
            PlayerPrefs.SetInt(PREF_KEY_AVATAR, avatarId);
            PlayerPrefs.SetString(PREF_KEY_GENDER, gender);
            PlayerPrefs.SetInt(PREF_KEY_AGE, age);
            PlayerPrefs.SetString(PREF_KEY_BIO, bio);
            PlayerPrefs.SetInt(PREF_KEY_LEVEL, level);
            PlayerPrefs.SetString(PREF_KEY_UID, uid);
            PlayerPrefs.SetString(PREF_KEY_REGION, region);
            PlayerPrefs.SetString(PREF_KEY_GUILD, guild);
            PlayerPrefs.SetString(PREF_KEY_TITLE, title);
            PlayerPrefs.SetInt(PREF_KEY_FLOWERS, flowers);
            PlayerPrefs.SetString(PREF_KEY_BIRTHDAY, birthday);
            PlayerPrefs.SetString(PREF_KEY_STATUS, status);
            PlayerPrefs.SetInt(PREF_KEY_COSTUME, costumeId);
            PlayerPrefs.Save();
        }

        public static string GenerateRandomNickname()
        {
            string[] prefixes = { "星际", "极光", "幻梦", "奥拉", "银河", "苍穹", "追光", "晨曦", "炽热", "灵动" };
            string[] nouns = { "漫游者", "小智", "旅行家", "冒险者", "守护者", "领航员", "寻梦人", "指挥官", "骑士", "旅人" };
            int randomNum = UnityEngine.Random.Range(100, 999);
            return $"{prefixes[UnityEngine.Random.Range(0, prefixes.Length)]}{nouns[UnityEngine.Random.Range(0, nouns.Length)]}_{randomNum}";
        }

        public static string GetGenderSymbol(string g)
        {
            return g switch
            {
                "male" => "♂",
                "female" => "♀",
                _ => "✦"
            };
        }

        public static string GetGenderColor(string g)
        {
            return g switch
            {
                "male" => "#40C4FF",
                "female" => "#FF4081",
                _ => "#B388FF"
            };
        }

        public static string GetGenderLabel(string g)
        {
            return g switch
            {
                "male" => "男生",
                "female" => "女生",
                _ => "保密"
            };
        }

        public static string GetAgeDisplay(int a)
        {
            return (a <= 0) ? "保密" : $"{a} 岁";
        }

        public UserProfile Clone()
        {
            return new UserProfile
            {
                username = this.username,
                avatarId = this.avatarId,
                gender = this.gender,
                age = this.age,
                bio = this.bio,
                level = this.level,
                uid = this.uid,
                region = this.region,
                guild = this.guild,
                title = this.title,
                flowers = this.flowers,
                achievePoints = this.achievePoints,
                residenceDays = this.residenceDays,
                friendsCount = this.friendsCount,
                costumeId = this.costumeId,
                birthday = this.birthday,
                status = this.status
            };
        }

    }
}
