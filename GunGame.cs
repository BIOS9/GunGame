using System;
using System.Collections.Generic;
using Rocket.Core.Plugins;
using SDG.Unturned;
using Rocket.Unturned.Player;
using UnityEngine;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using System.IO;
using Rocket.Unturned;
using Steamworks;
using System.Xml;
using Rocket.API.Collections;
using Rocket.Core.Commands;
using Rocket.API;
using System.Security.Cryptography;

namespace NightFish.GunGame
{
    public class GunGame : RocketPlugin
    {
        float EastWall = 50;
        float WestWall = -50;
        float NorthWall = 50;
        float SouthWall = -50;

        float WallEndY = 80;
        float WallStartY = 30;

        ushort WallBarricade = 1091;

        double WallBarricadeWidth = 3.75;
        double WallBarricadeHeight = 3.75;

        bool disableClearing = false;
        bool disableWallGen = false;
        bool disableUnstick = false;

        bool DisableGungame = false;
        bool DedicatedMode = true;

        bool UseRandomOrder = true;

        bool UseCustomSpawns = false;

        int UnstickDelay = 10;
        int UnstickCooldown = 120;

        int ResetDelay = 30;

        int MagRemoveCount = 0;

        bool error = false;

        List<int> weapons = new List<int>() { 107, 1021, 99, 1039, 448, 1369, 1379, 1041, 116, 1024, 1377, 484, 1000, 363, 1375, 122, 1362, 297, 1382, 112, 132, 1364 };

        Dictionary<ulong, int> gungameLevels = new Dictionary<ulong, int>();
        Dictionary<ulong, int> killstreak = new Dictionary<ulong, int>();
        Dictionary<ulong, DateTime> OP = new Dictionary<ulong, DateTime>();
        List<ulong> OPPlayers = new List<ulong>();
        List<int> WeaponList = new List<int>();
        Dictionary<ulong, Vector3> RespawnList = new Dictionary<ulong, Vector3>();
        Dictionary<ulong, DateTime> UnstickList = new Dictionary<ulong, DateTime>();
        Dictionary<ulong, DateTime> UnstickCooldownList = new Dictionary<ulong, DateTime>();
        Dictionary<string, Vector3> CustomSpawns = new Dictionary<string, Vector3>();

        DateTime ResetTime = DateTime.Now.AddMinutes(30);
        bool ResetOnTime = false;

        bool GameOver = false;
        ulong Winner = 0;

        DateTime lastBorderCheck = DateTime.Now;
        DateTime lastOPCheck = DateTime.Now;

        public override TranslationList DefaultTranslations
        {
            get
            {
                return new TranslationList()
                {
                    {"GAME_RESET","Server resetting in {0} seconds..."},
                    {"GAME_RESET_LOG", "Game Reset Triggered"},
                    {"GAME_RESET_START", "Game Resetting... Lag Incoming!"},
                    {"GAME_WIN","{0} has won this round of Gun Game!"},
                    {"LOST_OP", "{0} is no longer over powered."},
                    {"GAIN_OP", "{0} is Over Powered!"},
                    {"LVL_CHANGE", "You are on level {0}"},
                    {"INV_ERR", "There was an error clearing {0}'s inventory. Here is the error: {1}"},
                    {"ROUND_START", "ROUND STARTED!"},
                    {"ROUND_END", "ROUND ENDED!"},
                    {"VEH_CLR", "Cleared Vehicles"},
                    {"STRUCT_CLR", "Cleared Structures"},
                    {"WP_LIST_GEN", "Generated weapons list..."},
                    {"BARRIC_CLR", "Cleared barricades"},
                    {"ITEM_CLR", "Cleared Player Items"},
                    {"VEH_KICK", "Removed Players From Vehicles"},
                    {"LVL_RESET", "Reset Gun Game Levels"},
                    {"WALL_REBUILD", "Rebuilt Arena Wall"},
                    {"LVL_STEAL","{0} has stolen a level from {1}!"},
                    {"PLAYER_KILL","{0} has killed {1}!"},
                    {"KILLSTREAK","{0} has a kill streak of {1}!"},
                    {"KILLSTREAK_END","{0} has ended {1}'s kill streak of {2}"},
                    {"WALL_REBUILD_DISABLED","Arena wall build disabled... Skipping..."},
                    {"NO_UNSTICK", "Sorry, '/Unstick' is not enabled on this server!"},
                    {"UNSTICKING", "You will be unstuck in {0} seconds..."},
                    {"UNSTUCK", "You have been unstuck!"},
                    {"UNSTICK_COOLDOWN", "You have to wait {0} seconds before you can use this again!"},
                    {"SPAWN_ADD", "Spawnpoint '{3}' at {0}, {1}, {2} successfully added."},
                    {"SPAWN_ADD_ERR_NAME", "Invalid arguments! Please specify a name for this spawnpoint: '/as MySpawnName'"},
                    {"SPAWN_ADD_ERR_EXISTS", "Invalid arguments! A spawnpoint with this name already exists!"},
                    {"SPAWN_DEL", "Spawnpoint '{0}' successfully deleted."},
                    {"SPAWN_DEL_ERR_NAME", "Invalid arguments! Please specify the name of the spawn you want to delete: '/ds MySpawnName'"},
                    {"SPAWN_DEL_ERR_EXISTS", "Invalid arguments! A spawn with that name does not exist!"},
                    {"GUNGAME_DISABLED", "Sorry, Gun Game is currently disabled or not running!"},
                    {"GUNGAME_RUNNING", "Gun Game is already running, use '/ggstop' to stop the round."},
                    {"GUNGAME_STOPPED", "Gun Game is not already running, use '/ggstart' to start a round."},
                    {"GUNGAME_DEDICATED", "Gun Game must be running in non-dedicated mode to use this command!"},
                };
            }
        }

        public void saveXML()
        {
            if (File.Exists(@"plugins\gungame\GunGameConfig.xml"))
                File.Delete(@"plugins\gungame\GunGameConfig.xml");
            XmlWriterSettings xset = new XmlWriterSettings();
            xset.Indent = true;
            xset.NewLineOnAttributes = true;

            using (XmlWriter writer = XmlWriter.Create(@"plugins\gungame\GunGameConfig.xml", xset))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("GunGameConfiguration");
                writer.WriteElementString("TimeBetweenRounds", ResetDelay.ToString());
                writer.WriteStartElement("Border");
                writer.WriteComment("North wall must be bigger than South wall!");
                writer.WriteComment("East wall must be bigger than West wall!");
                writer.WriteElementString("WestWall", WestWall.ToString());
                writer.WriteElementString("EastWall", EastWall.ToString());
                writer.WriteElementString("SouthWall", SouthWall.ToString());
                writer.WriteElementString("NorthWall", NorthWall.ToString());
                writer.WriteComment("The settings below are to ajust the height and vertical location of the wall.");
                writer.WriteElementString("WallStartY", WallStartY.ToString());
                writer.WriteElementString("WallEndY", WallEndY.ToString());
                writer.WriteComment("The settings below are to customize the material that the wall is made of. The dimensions of the barricade is important. You can get some pretty interesting effects by modifying the width and height, try it.");
                writer.WriteComment("Width and height of large plates is: 3.75");
                writer.WriteComment("Width and height of small plates is: 2.0");
                writer.WriteElementString("WallBarricadeWidth", WallBarricadeWidth.ToString());
                writer.WriteElementString("WallBarricadeHeight", WallBarricadeHeight.ToString());
                writer.WriteElementString("WallBarricadeID", WallBarricade.ToString());
                writer.WriteComment("The following setting will disable the generation of the arena wall. Players will still not be able to leave.");
                writer.WriteElementString("DisableWall", disableWallGen.ToString());
                writer.WriteEndElement();
                writer.WriteStartElement("Unstick");
                writer.WriteElementString("DisableUnstick", disableUnstick.ToString());
                writer.WriteElementString("UnstickDelay", UnstickDelay.ToString());
                writer.WriteElementString("UnstickCooldown", UnstickCooldown.ToString());
                writer.WriteEndElement();
                writer.WriteStartElement("Misc");
                writer.WriteComment("The following setting will disable clearing barricades and structures. This setting should not be enabled while re-building walls is enabled because it will create walls on top of existing walls and cause lag.");
                writer.WriteElementString("DisableClear", disableClearing.ToString());
                writer.WriteEndElement();
                writer.WriteStartElement("Weapons");
                foreach (int w in weapons)
                {
                    writer.WriteElementString("Weapon", w.ToString());
                }
                writer.WriteElementString("UseRandomOrder", UseRandomOrder.ToString());
                writer.WriteEndElement();

                writer.WriteStartElement("CustomSpawnpoints");
                writer.WriteElementString("UseCustomSpawnpoints", UseCustomSpawns.ToString());
                foreach (KeyValuePair<string, Vector3> kvp in CustomSpawns)
                {
                    writer.WriteStartElement("CustomSpawn");
                    writer.WriteElementString("Name", kvp.Key);
                    writer.WriteElementString("X", kvp.Value.x.ToString());
                    writer.WriteElementString("Y", kvp.Value.y.ToString());
                    writer.WriteElementString("Z", kvp.Value.z.ToString());
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        protected override void Load()
        {
            if (!File.Exists(@"plugins\gungame\GunGameConfig.xml"))
            {
                saveXML();
            }
            else
            {
                bool ClearedWeapons = false;
                using (XmlReader reader = XmlReader.Create(@"plugins\gungame\GunGameConfig.xml"))
                {
                    string CurrentSpawnName = "";
                    double CurrentSpawnX = 0;
                    double CurrentSpawnY = 0;

                    while (reader.Read())
                    {
                        if (reader.IsStartElement())
                        {
                            switch (reader.Name)
                            {
                                case "WestWall":
                                    if (reader.Read())
                                    {
                                        WestWall = (float)Convert.ToDouble(reader.Value.Trim());
                                        Logger.Log("West wall set to: " + reader.Value.Trim());
                                    }
                                    break;
                                case "EastWall":
                                    if (reader.Read())
                                    {
                                        EastWall = (float)Convert.ToDouble(reader.Value.Trim());
                                        Logger.Log("East wall set to: " + reader.Value.Trim());
                                    }
                                    break;
                                case "SouthWall":
                                    if (reader.Read())
                                    {
                                        SouthWall = (float)Convert.ToDouble(reader.Value.Trim());
                                        Logger.Log("South wall set to: " + reader.Value.Trim());
                                    }
                                    break;
                                case "NorthWall":
                                    if (reader.Read())
                                    {
                                        NorthWall = (float)Convert.ToDouble(reader.Value.Trim());
                                        Logger.Log("North wall set to: " + reader.Value.Trim());
                                    }
                                    break;
                                case "Weapon":
                                    if (reader.Read())
                                    {
                                        if(!ClearedWeapons)
                                        {
                                            weapons = new List<int>();
                                            ClearedWeapons = true;
                                        }
                                        Logger.Log("Added weapon: " + reader.Value.Trim());
                                        weapons.Add(Convert.ToInt32(reader.Value.Trim()));
                                    }
                                    break;
                                case "TimeBetweenRounds":
                                    if (reader.Read())
                                    {
                                        Logger.Log("Time between rounds: " + reader.Value.Trim());
                                        ResetDelay = Convert.ToInt32(reader.Value.Trim());
                                    }
                                    break;
                                case "WallStartY":
                                    if (reader.Read())
                                    {
                                        Logger.Log("Wall Y pos start: " + reader.Value.Trim());
                                        WallStartY = (float)Convert.ToDouble(reader.Value.Trim());
                                    }
                                    break;
                                case "WallEndY":
                                    if (reader.Read())
                                    {
                                        Logger.Log("Wall Y pos end: " + reader.Value.Trim());
                                        WallEndY = (float)Convert.ToDouble(reader.Value.Trim());
                                    }
                                    break;
                                case "DisableWall":
                                    if (reader.Read())
                                    {
                                        Logger.Log("Disable wall generation: " + reader.Value.Trim());
                                        disableWallGen = Convert.ToBoolean(reader.Value.Trim());
                                    }
                                    break;
                                case "WallBarricadeWidth":
                                    if (reader.Read())
                                    {
                                        Logger.Log("Wall barricade width: " + reader.Value.Trim());
                                        WallBarricadeWidth = Convert.ToDouble(reader.Value.Trim());
                                    }
                                    break;
                                case "WallBarricadeHeight":
                                    if (reader.Read())
                                    {
                                        Logger.Log("Wall barricade height: " + reader.Value.Trim());
                                        WallBarricadeHeight = Convert.ToDouble(reader.Value.Trim());
                                    }
                                    break;
                                case "WallBarricadeID":
                                    if (reader.Read())
                                    {
                                        Logger.Log("Wall barricade ID: " + reader.Value.Trim());
                                        WallBarricade = Convert.ToUInt16(reader.Value.Trim());
                                    }
                                    break;
                                case "DisableClear":
                                    if (reader.Read())
                                    {
                                        Logger.Log("Disable barricade/structure clearing: " + reader.Value.Trim());
                                        disableClearing = Convert.ToBoolean(reader.Value.Trim());
                                    }
                                    break;
                                case "DisableUnstick":
                                    if (reader.Read())
                                    {
                                        Logger.Log("Disable unstick command: " + reader.Value.Trim());
                                        disableUnstick = Convert.ToBoolean(reader.Value.Trim());
                                    }
                                    break;
                                case "UnstickDelay":
                                    if (reader.Read())
                                    {
                                        Logger.Log("Unstick delay: " + reader.Value.Trim() + " seconds");
                                        UnstickDelay = Convert.ToInt32(reader.Value.Trim());
                                    }
                                    break;
                                case "UnstickCooldown":
                                    if (reader.Read())
                                    {
                                        Logger.Log("Unstick cooldown: " + reader.Value.Trim() + " seconds");
                                        UnstickCooldown = Convert.ToInt32(reader.Value.Trim());
                                    }
                                    break;
                                case "UseCustomSpawnpoints":
                                    if (reader.Read())
                                    {
                                        Logger.Log("Use custom spawnpoints: " + reader.Value.Trim());
                                        UseCustomSpawns = Convert.ToBoolean(reader.Value.Trim());
                                    }
                                    break;
                                case "Name":
                                    if (reader.Read())
                                    {
                                        CurrentSpawnName = Convert.ToString(reader.Value.Trim());
                                    }
                                    break;
                                case "X":
                                    if (reader.Read())
                                    {
                                        CurrentSpawnX = Convert.ToDouble(reader.Value.Trim());
                                    }
                                    break;
                                case "Y":
                                    if (reader.Read())
                                    {
                                        CurrentSpawnY = Convert.ToDouble(reader.Value.Trim());
                                    }
                                    break;
                                case "Z":
                                    if (reader.Read())
                                    {
                                        CustomSpawns.Add(CurrentSpawnName, new Vector3((float)CurrentSpawnX, (float)CurrentSpawnY, (float)Convert.ToDouble(reader.Value.Trim())));
                                        Logger.Log("Custom spawn: " + CurrentSpawnName + " at: X:" + CurrentSpawnX + ", Y:" + CurrentSpawnY + ", Z:" + Convert.ToDouble(reader.Value.Trim()));
                                        CurrentSpawnName = "";
                                        CurrentSpawnX = 0;
                                        CurrentSpawnY = 0;
                                    }
                                    break;
                                case "UseRandomOrder":
                                    if(reader.Read())
                                    {
                                        Logger.Log("Random Weapon Order: " + Convert.ToBoolean(reader.Value.Trim()));
                                        UseRandomOrder = Convert.ToBoolean(reader.Value.Trim());
                                    }
                                    break;
                            }
                        }
                    }
                }

                if (EastWall <= WestWall)
                {
                    Logger.LogError("East wall must be bigger than West wall! Please fix this error to continue!");
                    error = true;
                }
                if (NorthWall <= SouthWall)
                {
                    Logger.LogError("North wall must be bigger than South wall! Please fix this error to continue!");
                    error = true;
                }
            }

            saveXML(); //Used to update old config files while keeping original settings

            if (UseCustomSpawns)
            {
                if(CustomSpawns.Count == 0)
                {
                    Logger.LogError("No custom spawn points set! Defaulting back to random spawns...");
                    UseCustomSpawns = false;
                }
            }

            Rocket.Unturned.Events.UnturnedPlayerEvents.OnPlayerDeath += PlayerDeath;
            Rocket.Unturned.Events.UnturnedPlayerEvents.OnPlayerRevive += PlayerRespawn;
            U.Events.OnPlayerConnected += PlayerJoin;
            
            GenerateWeaponList();
            gungameLevels.Clear();
            killstreak.Clear();
            OP.Clear();

            foreach (SteamPlayer sp in Provider.clients)
            {
                UnturnedPlayer player = UnturnedPlayer.FromSteamPlayer(sp);
                gungameLevels.Add(player.CSteamID.m_SteamID, 0);
                killstreak.Add(player.CSteamID.m_SteamID, 0);
                ClearInv(player);
                ClearClothes(player);
            }
        }

        protected override void Unload()
        {
            Rocket.Unturned.Events.UnturnedPlayerEvents.OnPlayerDeath -= PlayerDeath;
            Rocket.Unturned.Events.UnturnedPlayerEvents.OnPlayerRevive -= PlayerRespawn;
            U.Events.OnPlayerConnected -= PlayerJoin;
        }

        public int Rand(int minValue, int maxValue)
        {
            RNGCryptoServiceProvider _rng = new RNGCryptoServiceProvider();
            if (minValue > maxValue)
                throw new ArgumentOutOfRangeException("minValue");
            if (minValue == maxValue) return minValue;
            Int64 diff = maxValue - minValue;
            while (true)
            {
                byte[] _uint32Buffer = new byte[4];
                _rng.GetBytes(_uint32Buffer);
                UInt32 rand = BitConverter.ToUInt32(_uint32Buffer, 0);

                Int64 max = (1 + (Int64)UInt32.MaxValue);
                Int64 remainder = max % diff;
                if (rand < max - remainder)
                {
                    return (Int32)(minValue + (rand % diff));
                }
            }
        }

        public void PlayerDeath(UnturnedPlayer player, EDeathCause cause, ELimb limb, CSteamID murderer)
        {
            if (error || DisableGungame)
                return;
            OP.Remove(player.CSteamID.m_SteamID);
            bool killedByPlayer = false;
            switch (cause)
            {
                case EDeathCause.GRENADE:
                    killedByPlayer = true;
                    break;
                case EDeathCause.GUN:
                    killedByPlayer = true;
                    break;
                case EDeathCause.KILL:
                    killedByPlayer = true;
                    break;
                case EDeathCause.MELEE:
                    killedByPlayer = true;
                    break;
                case EDeathCause.PUNCH:
                    killedByPlayer = true;
                    break;
            }

            if (killedByPlayer)
            {
                try
                {
                    UnturnedPlayer killer = UnturnedPlayer.FromCSteamID(murderer);

                    int ks = 0;
                    if (killstreak.TryGetValue(player.CSteamID.m_SteamID, out ks))
                    {
                        if (ks > 3)
                        {
                            killstreak.Remove(player.CSteamID.m_SteamID);
                            UnturnedChat.Say(DefaultTranslations.Translate("KILLSTREAK_END", killer.DisplayName, player.DisplayName, ks), Color.red);
                        }
                    }

                    ks = 1;
                    if (killstreak.TryGetValue(killer.CSteamID.m_SteamID, out ks))
                    {
                        killstreak.Remove(killer.CSteamID.m_SteamID);
                    }
                    killstreak.Add(killer.CSteamID.m_SteamID, ks + 1);

                    if (ks == 2)
                    {
                        if (!OP.ContainsKey(killer.CSteamID.m_SteamID))
                        {
                            OP.Add(killer.CSteamID.m_SteamID, DateTime.Now.AddSeconds(5));
                            UnturnedChat.Say(DefaultTranslations.Translate("KILLSTREAK", killer.DisplayName, ks.ToString()), Color.red);
                        }
                    }
                    else if (ks == 4)
                    {
                        if (!OP.ContainsKey(killer.CSteamID.m_SteamID))
                        {
                            OP.Add(killer.CSteamID.m_SteamID, DateTime.Now.AddSeconds(10));
                            UnturnedChat.Say(DefaultTranslations.Translate("KILLSTREAK", killer.DisplayName, ks.ToString()), Color.red);
                        }
                    }
                    else if (ks == 9)
                    {
                        if (!OP.ContainsKey(killer.CSteamID.m_SteamID))
                        {
                            OP.Add(killer.CSteamID.m_SteamID, DateTime.Now.AddSeconds(20));
                            UnturnedChat.Say(DefaultTranslations.Translate("KILLSTREAK", killer.DisplayName, ks.ToString()), Color.red);
                        }
                    }
                    else if (ks == 19)
                    {
                        if (!OP.ContainsKey(killer.CSteamID.m_SteamID))
                        {
                            OP.Add(killer.CSteamID.m_SteamID, DateTime.Now.AddSeconds(60));
                            UnturnedChat.Say(DefaultTranslations.Translate("KILLSTREAK", killer.DisplayName, ks.ToString()), Color.red);
                        }
                    }

                    int pastLevel = 0;
                    try
                    {
                        pastLevel = gungameLevels[killer.CSteamID.m_SteamID];
                    }
                    catch { }
                    gungameLevels.Remove(killer.CSteamID.m_SteamID);
                    gungameLevels.Add(killer.CSteamID.m_SteamID, pastLevel + 1);
                    UnturnedChat.Say(DefaultTranslations.Translate("PLAYER_KILL", killer.DisplayName, player.DisplayName), Color.grey);
                    if (cause == EDeathCause.MELEE)
                    {
                        killer.Heal(50);
                        UnturnedChat.Say(DefaultTranslations.Translate("LVL_STEAL", killer.DisplayName, player.DisplayName), Color.blue);
                        int pastDeathLevel = 0;
                        try
                        {
                            pastDeathLevel = gungameLevels[player.CSteamID.m_SteamID];
                        }
                        catch { }
                        if (pastLevel > 0)
                        {
                            gungameLevels.Remove(player.CSteamID.m_SteamID);
                            gungameLevels.Add(player.CSteamID.m_SteamID, pastDeathLevel - 1);
                        }
                        int playerLevel = 0;
                        try
                        {
                            playerLevel = gungameLevels[player.CSteamID.m_SteamID];
                        }
                        catch
                        { }
                    }
                    else
                    {
                        killer.Heal(15);
                    }
                }
                catch
                { }
            }
            else
            {
                killstreak.Remove(player.CSteamID.m_SteamID);
            }
        }

        public void PlayerRespawn(UnturnedPlayer player, Vector3 position, byte angle)
        {
            if (error || DisableGungame)
                return;
            if (UseCustomSpawns)
            {
                Vector3 pos = new Vector3(0, 0, 0);
                int index = Rand(0, CustomSpawns.Count);
                int i = 0;
                foreach(KeyValuePair<string, Vector3> kvp in CustomSpawns)
                {
                    if(i == index)
                    {
                        RespawnList.Add(player.CSteamID.m_SteamID, kvp.Value);
                    }
                    i++;
                }
            }
            else
            { 
                System.Random r = new System.Random(DateTime.Now.Millisecond);
                int x = r.Next((int)WestWall, (int)EastWall);
                int z = r.Next((int)SouthWall, (int)NorthWall);
                RespawnList.Add(player.CSteamID.m_SteamID, new Vector3(x, -10, z));
            }

            int playerLevel = 0;
            try
            {
                playerLevel = gungameLevels[player.CSteamID.m_SteamID];
            }
            catch
            { }
            player.GiveItem(new Item(121, true));
            UnturnedChat.Say(player, DefaultTranslations.Translate("LVL_CHANGE", (playerLevel + 1)), Color.cyan);
        }

        public void PlayerJoin(UnturnedPlayer player)
        {
            if (error)
                return;
            ClearClothes(player);
            ClearInv(player);

            if (UseCustomSpawns)
            {
                Vector3 pos = new Vector3(0, 0, 0);
                int index = Rand(0, CustomSpawns.Count);
                int i = 0;
                foreach (KeyValuePair<string, Vector3> kvp in CustomSpawns)
                {
                    if (i == index)
                    {
                        try
                        { 
                            RespawnList.Add(player.CSteamID.m_SteamID, kvp.Value);
                        }
                        catch { }
                    }
                    i++;
                }
            }
            else
            {
                System.Random r = new System.Random(DateTime.Now.Millisecond);
                int x = r.Next((int)WestWall, (int)EastWall);
                int z = r.Next((int)SouthWall, (int)NorthWall);
                try
                { 
                    RespawnList.Add(player.CSteamID.m_SteamID, new Vector3(x, -10, z));
                }
                catch { }
            }
/*
            int playerLevel = 0;
            try
            {
                playerLevel = gungameLevels[player.CSteamID.m_SteamID];
            }
            catch
            { }*/
        }

        public void GenerateWeaponList()
        {
            if (error)
                return;
            WeaponList = weapons;
            if(UseRandomOrder)
                WeaponList.Shuffle();

            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine(DefaultTranslations.Translate("WP_LIST_GEN"));
            Console.ForegroundColor = ConsoleColor.White;
        }

        public void RebuildWall()
        {
            if (error)
                return;
            if (!disableClearing)
            {
                BarricadeManager.askClearAllBarricades();
                StructureManager.askClearAllStructures();
                Logger.LogWarning(DefaultTranslations.Translate("BARRIC_CLR"));
                Logger.LogWarning(DefaultTranslations.Translate("STRUCT_CLR"));
            }
            if (!disableWallGen)
            {
                Barricade b = new Barricade(WallBarricade);
                Transform tr = new GameObject().transform;
                Vector3 pos = new Vector3(0, 100, 0);
                for (float y = WallStartY; y < WallEndY; y += (float)WallBarricadeHeight)
                {
                    for (float z = SouthWall; z < NorthWall + WallBarricadeWidth; z += (float)WallBarricadeWidth)
                    {
                        pos = new Vector3(WestWall, y, z);
                        BarricadeManager.dropBarricade(b, tr, pos, 0, 90, 0, 000, 000);
                        pos = new Vector3(EastWall, y, z);
                        BarricadeManager.dropBarricade(b, tr, pos, 0, 90, 0, 000, 000);
                    }
                    for (float x = WestWall; x < EastWall + WallBarricadeWidth; x += (float)WallBarricadeWidth)
                    {
                        pos = new Vector3(x, y, SouthWall);
                        BarricadeManager.dropBarricade(b, tr, pos, 0, 0, 0, 000, 000);
                        pos = new Vector3(x, y, NorthWall);
                        BarricadeManager.dropBarricade(b, tr, pos, 0, 0, 0, 000, 000);
                    }
                }
                Logger.LogWarning(DefaultTranslations.Translate("WALL_REBUILD"));
            }
            else
            {
                Logger.LogWarning(DefaultTranslations.Translate("WALL_REBUILD_DISABLED"));
            }
        }

        public void ResetGame()
        {
            if (error)
                return;
            ResetTime = DateTime.Now.AddMinutes(30);
            Winner = 0;
            GameOver = false;
            UnturnedChat.Say(DefaultTranslations.Translate("GAME_RESET_START"), Color.yellow);
            Logger.LogWarning(DefaultTranslations.Translate("GAME_RESET_LOG"));
            gungameLevels.Clear();
            killstreak.Clear();
            OP.Clear();
            Logger.LogWarning(DefaultTranslations.Translate("LVL_RESET"));
            foreach (SteamPlayer sp in Provider.clients)
            {
                try
                {
                    UnturnedPlayer player = UnturnedPlayer.FromSteamPlayer(sp);
                    gungameLevels.Add(player.CSteamID.m_SteamID, 0);
                    killstreak.Add(player.CSteamID.m_SteamID, 0);
                    GameObject.Find("Managers").GetComponent<VehicleManager>().askExitVehicle(player.CSteamID, Vector3.zero);
                    ClearInv(player);
                    ClearClothes(player);
                    player.Heal(100);
                }
                catch { }
            }
            Logger.LogWarning(DefaultTranslations.Translate("VEH_KICK"));
            Logger.LogWarning(DefaultTranslations.Translate("ITEM_CLR"));
            RebuildWall();
            VehicleManager.askVehicleDestroyAll();
            Logger.LogWarning(DefaultTranslations.Translate("VEH_CLR"));
            GenerateWeaponList();
            UnturnedChat.Say(DefaultTranslations.Translate("ROUND_START"), Color.magenta);
            UnturnedChat.Say(DefaultTranslations.Translate("LVL_CHANGE", 1), Color.cyan);
            int seed = 0;
            foreach (SteamPlayer sp in Provider.clients)
            {
                UnturnedPlayer player = UnturnedPlayer.FromSteamPlayer(sp);
                if (UseCustomSpawns)
                {
                    Vector3 pos = new Vector3(0, 0, 0);
                    int index = Rand(0, CustomSpawns.Count);
                    int i = 0;
                    foreach (KeyValuePair<string, Vector3> kvp in CustomSpawns)
                    {
                        if (i == index)
                        {
                            try
                            {
                                RespawnList.Add(player.CSteamID.m_SteamID, kvp.Value);
                            }
                            catch { }
                        }
                        i++;
                    }
                }
                else
                {
                    System.Random r = new System.Random(DateTime.Now.Millisecond + seed);
                    int x = r.Next((int)WestWall, (int)EastWall);
                    int z = r.Next((int)SouthWall, (int)NorthWall);
                    try
                    { 
                    RespawnList.Add(player.CSteamID.m_SteamID, new Vector3(x, -10, z));
                    }
                    catch { }
                }
                seed++;
            }
        }

        public void MaxSkills(UnturnedPlayer player)
        {
            if (error)
                return;
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Agriculture, 255);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Cooking, 255);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Crafting, 255);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Dexerity, 255);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Diving, 255);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Fishing, 255);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Healing, 255);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Immunity, 255);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Mechanic, 255);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Outdoors, 255);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Overkill, 255);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Parkour, 255);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Sharpshooter, 255);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Sneakybeaky, 255);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Strength, 255);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Survival, 255);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Toughness, 255);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Vitality, 255);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Warmblooded, 255);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Engineer, 255);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Exercise, 255);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Cardio, 255);
        }

        public void MinSkills(UnturnedPlayer player)
        {
            if (error)
                return;
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Agriculture, 0);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Cooking, 0);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Crafting, 0);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Dexerity, 0);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Diving, 0);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Fishing, 0);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Healing, 0);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Immunity, 0);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Mechanic, 0);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Outdoors, 0);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Overkill, 0);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Parkour, 0);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Sharpshooter, 0);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Sneakybeaky, 0);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Strength, 0);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Survival, 0);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Toughness, 0);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Vitality, 0);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Warmblooded, 0);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Engineer, 0);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Exercise, 0);
            player.SetSkillLevel(Rocket.Unturned.Skills.UnturnedSkill.Cardio, 0);
        }

        void FixedUpdate()
        {
            if (error || DisableGungame)
                return;
            if ((DateTime.Now - lastOPCheck).TotalMilliseconds > 300)
            {
                List<ulong> deleteList = new List<ulong>();
                int seed = 0;
                foreach(KeyValuePair<ulong, DateTime> unstickers in UnstickList)
                {
                    if(unstickers.Value < DateTime.Now)
                    {
                        UnturnedPlayer player = UnturnedPlayer.FromCSteamID(new CSteamID(unstickers.Key));
                        if (UseCustomSpawns)
                        {
                            Vector3 pos = new Vector3(0, 0, 0);
                            int index = Rand(0, CustomSpawns.Count);
                            int i = 0;
                            foreach (KeyValuePair<string, Vector3> kvp in CustomSpawns)
                            {
                                if (i == index)
                                {
                                    RespawnList.Add(player.CSteamID.m_SteamID, kvp.Value);
                                }
                                i++;
                            }
                        }
                        else
                        {
                            System.Random r = new System.Random(DateTime.Now.Millisecond + seed);
                            int x = r.Next((int)WestWall, (int)EastWall);
                            int z = r.Next((int)SouthWall, (int)NorthWall);
                            RespawnList.Add(player.CSteamID.m_SteamID, new Vector3(x, -10, z));
                        }
                        UnturnedChat.Say(player, DefaultTranslations.Translate("UNSTUCK"), Color.green);
                        deleteList.Add(unstickers.Key);
                        seed++;
                    }
                }

                foreach (ulong delete in deleteList)
                {
                    UnstickList.Remove(delete);
                }
                deleteList.Clear();

                foreach (KeyValuePair<ulong, DateTime> USCooldown in UnstickCooldownList)
                {
                    if (USCooldown.Value < DateTime.Now)
                    {
                        UnturnedPlayer player = UnturnedPlayer.FromCSteamID(new CSteamID(USCooldown.Key));
                        deleteList.Add(USCooldown.Key);
                    }
                }

                foreach (ulong delete in deleteList)
                {
                    UnstickCooldownList.Remove(delete);
                }

                if (ResetOnTime == true)
                {
                    if (DateTime.Now > ResetTime)
                    {
                        ResetOnTime = false;
                        ResetGame();
                    }
                }

                foreach (SteamPlayer sp in Provider.clients)
                {
                    UnturnedPlayer player = UnturnedPlayer.FromSteamPlayer(sp);
                    if (!GameOver)
                    {
                        if (OPPlayers.Contains(player.CSteamID.m_SteamID))
                        {
                            MaxSkills(player);
                            if (player.Player.clothing.hat != 1385)
                            {
                                player.Player.clothing.askWearHat(1385, 100, new byte[0], true);
                            }
                            if (player.Player.clothing.vest != 1169)
                            {
                                player.Player.clothing.askWearVest(1169, 100, new byte[0], true);
                            }
                            if (player.Player.clothing.shirt != 1171)
                            {
                                player.Player.clothing.askWearShirt(1171, 100, new byte[0], true);
                            }
                            if (player.Player.clothing.pants != 1172)
                            {
                                player.Player.clothing.askWearPants(1172, 100, new byte[0], true);
                            }
                        }
                        else
                        {
                            MinSkills(player);
                            try
                            {
                                player.Player.clothing.askWearBackpack(0, 0, new byte[0], true);
                                player.Player.clothing.askWearGlasses(0, 0, new byte[0], true);
                                player.Player.clothing.askWearHat(0, 0, new byte[0], true);
                                player.Player.clothing.askWearMask(0, 0, new byte[0], true);
                                player.Player.clothing.askWearPants(0, 0, new byte[0], true);
                                player.Player.clothing.askWearShirt(0, 0, new byte[0], true);
                                player.Player.clothing.askWearVest(0, 0, new byte[0], true);
                                for (byte p2 = 1; p2 < player.Player.inventory.getItemCount(2); p2++)
                                {
                                    player.Player.inventory.removeItem(2, 1);
                                }
                            }
                            catch (Exception e)
                            {
                                Logger.Log(DefaultTranslations.Translate("INV_ERR", player.CharacterName, e.Message));
                            }
                        }
                    }
                    try
                    {
                        GameObject.Find("Managers").GetComponent<VehicleManager>().askExitVehicle(player.CSteamID, Vector3.zero);
                        VehicleManager.askVehicleDestroyAll();
                    }
                    catch
                    { }
                }
                lastOPCheck = DateTime.Now;
            }

            if ((DateTime.Now - lastBorderCheck).TotalSeconds > 0.2)
            {
                MagRemoveCount++;
                if (MagRemoveCount > 20)
                {
                    foreach (SteamPlayer sp in Provider.clients)
                    {
                        try
                        {
                            UnturnedPlayer player = UnturnedPlayer.FromSteamPlayer(sp);
                            player.Inventory.removeItem(2, 0);
                            MagRemoveCount = 0;
                        }
                        catch
                        {
                        }
                    }
                }

                List<ulong> deleteOP = new List<ulong>();
                foreach (KeyValuePair<ulong, DateTime> _op in OP)
                {
                    if (_op.Value < DateTime.Now)
                    {
                        deleteOP.Add(_op.Key);
                        OPPlayers.Remove(_op.Key);
                    }
                    else
                    {
                        if (!OPPlayers.Contains(_op.Key))
                        {
                            OPPlayers.Add(_op.Key);
                            UnturnedChat.Say(DefaultTranslations.Translate("GAIN_OP", UnturnedPlayer.FromCSteamID(new CSteamID(_op.Key)).DisplayName), Color.magenta);
                        }
                    }
                }

                foreach (ulong u in deleteOP)
                {
                    OP.Remove(u);
                    UnturnedChat.Say(DefaultTranslations.Translate("LOST_OP", UnturnedPlayer.FromCSteamID(new CSteamID(u)).DisplayName), Color.yellow);
                }

                deleteOP.Clear();

                foreach (ulong u in OPPlayers)
                {
                    if (!OP.ContainsKey(u))
                    {
                        deleteOP.Add(u);
                    }
                }

                foreach (ulong u in deleteOP)
                {
                    OPPlayers.Remove(u);
                    UnturnedChat.Say(DefaultTranslations.Translate("LOST_OP", UnturnedPlayer.FromCSteamID(new CSteamID(u)).DisplayName), Color.yellow);
                }

                foreach (SteamPlayer sp in Provider.clients)
                {
                    try
                    {
                        UnturnedPlayer player = UnturnedPlayer.FromSteamPlayer(sp);

                        if (RespawnList.ContainsKey(player.CSteamID.m_SteamID))
                        {
                            player.Teleport(RespawnList[player.CSteamID.m_SteamID], 0);
                            RespawnList.Remove(player.CSteamID.m_SteamID);
                        }
                        else
                        {
                            if (!player.IsAdmin)
                            {
                                if (player.Position.x < WestWall)
                                {
                                    player.Teleport(new Vector3(WestWall + (float)2, player.Position.y, player.Position.z), player.Rotation);
                                }
                                if (player.Position.x > EastWall)
                                {
                                    player.Teleport(new Vector3(EastWall - (float)2, player.Position.y, player.Position.z), player.Rotation);
                                }
                                if (player.Position.z < SouthWall)
                                {
                                    player.Teleport(new Vector3(player.Position.x, player.Position.y, SouthWall + (float)2), player.Rotation);
                                }
                                if (player.Position.z > NorthWall)
                                {
                                    player.Teleport(new Vector3(player.Position.x, player.Position.y, NorthWall - (float)2), player.Rotation);
                                }
                            }
                        }

                        if (!gungameLevels.ContainsKey(player.CSteamID.m_SteamID))
                        {
                            gungameLevels.Add(player.CSteamID.m_SteamID, 0);
                        }

                        int PlayerLevel = gungameLevels[player.CSteamID.m_SteamID];
                        int index = 0;
                        int LevelGun = 0;

                        foreach (int gun in WeaponList)
                        {
                            if (index == PlayerLevel)
                            {
                                LevelGun = gun;
                            }
                            index += 1;
                        }

                        if (PlayerLevel == WeaponList.Count)
                        {
                            LevelGun = 121;
                        }
                        else if (PlayerLevel > WeaponList.Count)
                        {
                            if (!DedicatedMode)
                            {
                                DisableGungame = true;
                                foreach (SteamPlayer sp1 in Provider.clients)
                                {
                                    UnturnedPlayer p1 = UnturnedPlayer.FromSteamPlayer(sp1);
                                    ClearInv(p1);
                                    ClearClothes(p1);
                                    p1.Heal(100);
                                }

                                if (!disableClearing)
                                {
                                    BarricadeManager.askClearAllBarricades();
                                    StructureManager.askClearAllStructures();
                                    Logger.LogWarning(DefaultTranslations.Translate("BARRIC_CLR"));
                                    Logger.LogWarning(DefaultTranslations.Translate("STRUCT_CLR"));
                                }
                                Winner = player.CSteamID.m_SteamID;
                                UnturnedChat.Say(DefaultTranslations.Translate("GAME_WIN", player.DisplayName), Color.cyan);
                                UnturnedChat.Say(DefaultTranslations.Translate("ROUND_END"), Color.magenta);
                            }
                            else
                            {
                                if (GameOver == false)
                                {
                                    GameOver = true;
                                    Winner = player.CSteamID.m_SteamID;
                                    UnturnedChat.Say(DefaultTranslations.Translate("GAME_WIN", player.DisplayName), Color.cyan);
                                    ResetTime = DateTime.Now.AddSeconds(ResetDelay);
                                    ResetOnTime = true;
                                    UnturnedChat.Say(DefaultTranslations.Translate("GAME_RESET", ResetDelay.ToString()), Color.gray);
                                }
                            }
                        }
                        else if (PlayerLevel < 0)
                        {
                            gungameLevels.Remove(player.CSteamID.m_SteamID);
                            gungameLevels.Add(player.CSteamID.m_SteamID, 0);
                            PlayerLevel = 0;
                        }

                        if (!GameOver)
                        {
                            Items i = new Items(1);
                            i.addItem(0, 0, 0, new Item(121, true));
                            player.Inventory.replaceItems(1, i);
                            try
                            {
                                if (player.Player.equipment.equippedPage != 0 && player.Player.equipment.equippedPage != 1)
                                {
                                    player.Player.equipment.tryEquip(0, 0, 0);
                                    player.Inventory.removeItem(0, 0);
                                }
                            }
                            catch
                            { }

                            player.Inventory.tryAddItem(new Item((ushort)LevelGun, true), 0, 0, 0, 0);
                            try
                            {
                                if (player.Inventory.getItem(0, 0).item != null)
                                {
                                    if (player.Inventory.getItem(0, 0).item.id != LevelGun)
                                    {
                                        player.Inventory.removeItem(0, 0);
                                        UnturnedChat.Say(player, DefaultTranslations.Translate("LVL_CHANGE", PlayerLevel + 1), Color.cyan);
                                    }
                                }
                                else
                                {
                                }
                            }
                            catch
                            { }

                            try
                            {
                                ItemGunAsset currentWeapon = (ItemGunAsset)player.Player.equipment.asset;
                                if (player.Player.equipment.asset != null && currentWeapon != null)
                                {
                                    if (MagRemoveCount > 100)
                                    {
                                        MagRemoveCount = 0;
                                    }
                                    player.Inventory.tryAddItem(new Item(currentWeapon.magazineID, true), 0, 0, 2, 0);
                                }

                                if (player.Inventory.getItem(2, 0).item.id != currentWeapon.magazineID)
                                {
                                    player.Inventory.removeItem(2, 0);
                                }
                            }
                            catch
                            {
                                player.Inventory.removeItem(2, 0);
                            }
                        }
                        else
                        {
                            player.Heal(20);
                            if (player.CSteamID.m_SteamID != Winner)
                            {
                                ClearInv(player);
                                ClearClothes(player);
                            }
                            else
                            {
                                ClearInv(player);
                                ClearClothes(player);
                                MaxSkills(player);
                            }
                        }
                    }
                    catch
                    { }
                    lastBorderCheck = DateTime.Now;
                }
            }
        }

        public void ClearInv(UnturnedPlayer player)
        {
            if (error)
                return;
            try
            {
                var playerInv = player.Inventory;
                for (byte page = 0; page < 8; page++)
                {
                    var count = playerInv.getItemCount(page);

                    for (byte index = 0; index < count; index++)
                    {
                        playerInv.removeItem(page, 0);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log(DefaultTranslations.Translate("INV_ERR", player.CharacterName, e.Message));
            }
        }

        public void ClearClothes(UnturnedPlayer player)
        {
            if (error)
                return;
            try
            {
                player.Player.clothing.askWearBackpack(0, 0, new byte[0], true);
                for (byte p2 = 0; p2 < player.Player.inventory.getItemCount(2); p2++)
                {
                    player.Player.inventory.removeItem(2, 0);
                }
                player.Player.clothing.askWearGlasses(0, 0, new byte[0], true);
                for (byte p2 = 0; p2 < player.Player.inventory.getItemCount(2); p2++)
                {
                    player.Player.inventory.removeItem(2, 0);
                }
                player.Player.clothing.askWearHat(0, 0, new byte[0], true);
                for (byte p2 = 0; p2 < player.Player.inventory.getItemCount(2); p2++)
                {
                    player.Player.inventory.removeItem(2, 0);
                }
                player.Player.clothing.askWearMask(0, 0, new byte[0], true);
                for (byte p2 = 0; p2 < player.Player.inventory.getItemCount(2); p2++)
                {
                    player.Player.inventory.removeItem(2, 0);
                }
                player.Player.clothing.askWearPants(0, 0, new byte[0], true);
                for (byte p2 = 0; p2 < player.Player.inventory.getItemCount(2); p2++)
                {
                    player.Player.inventory.removeItem(2, 0);
                }
                player.Player.clothing.askWearShirt(0, 0, new byte[0], true);
                for (byte p2 = 0; p2 < player.Player.inventory.getItemCount(2); p2++)
                {
                    player.Player.inventory.removeItem(2, 0);
                }
                player.Player.clothing.askWearVest(0, 0, new byte[0], true);
                for (byte p2 = 0; p2 < player.Player.inventory.getItemCount(2); p2++)
                {
                    player.Player.inventory.removeItem(2, 0);
                }
            }
            catch (Exception e)
            {
                Logger.Log(DefaultTranslations.Translate("INV_ERR", player.CharacterName, e.Message));
            }
        }

        #region Commands

        #region Arena Wall Commands

        [RocketCommand("eastwall", "", "", AllowedCaller.Both)] //Sets the east wall parameter of the arena wall
        [RocketCommandAlias("ew")]
        public void ExecuteCommandMaxx(IRocketPlayer caller, string[] parameters)
        {
            if(DisableGungame)
            {
                UnturnedChat.Say(caller, DefaultTranslations.Translate("GUNGAME_DISABLED"), Color.yellow);
                return;
            }

            try
            {

                int Num = Convert.ToInt32(parameters[0]);
                if (Num <= WestWall)
                {
                    UnturnedChat.Say(caller, "East wall must be bigger than West wall!");
                }
                else
                {
                    EastWall = Num;
                    saveXML();
                    UnturnedChat.Say(caller, "East wall set to: " + Num.ToString());
                    RebuildWall();
                }
            }
            catch
            {
                UnturnedChat.Say(caller, "Invalid parameters!");
            }
        }

        [RocketCommand("westwall", "", "", AllowedCaller.Both)] //Sets the west wall parameter of the arena wall
        [RocketCommandAlias("ww")]
        public void ExecuteCommandMinx(IRocketPlayer caller, string[] parameters)
        {
            if (DisableGungame)
            {
                UnturnedChat.Say(caller, DefaultTranslations.Translate("GUNGAME_DISABLED"), Color.yellow);
                return;
            }

            try
            {
                int Num = Convert.ToInt32(parameters[0]);
                if (Num >= EastWall)
                {
                    UnturnedChat.Say(caller, "West wall must be smaller than East wall!");
                }
                else
                {
                    WestWall = Num;
                    saveXML();
                    UnturnedChat.Say(caller, "West wall set to: " + Num.ToString());
                    RebuildWall();
                }
            }
            catch
            {
                UnturnedChat.Say(caller, "Invalid parameters!");
            }
        }

        [RocketCommand("northwall", "", "", AllowedCaller.Both)] //Sets the north wall parameter of the arena wall
        [RocketCommandAlias("nw")]
        public void ExecuteCommandMaxz(IRocketPlayer caller, string[] parameters)
        {
            if (DisableGungame)
            {
                UnturnedChat.Say(caller, DefaultTranslations.Translate("GUNGAME_DISABLED"), Color.yellow);
                return;
            }

            try
            {
                int Num = Convert.ToInt32(parameters[0]);
                if (Num <= SouthWall)
                {
                    UnturnedChat.Say(caller, "North wall must be bigger than South wall!");
                }
                else
                {
                    NorthWall = Convert.ToInt32(Num);
                    saveXML();
                    UnturnedChat.Say(caller, "North wall set to: " + Num.ToString());
                    RebuildWall();
                }
            }
            catch
            {
                UnturnedChat.Say(caller, "Invalid parameters!");
            }
        }

        [RocketCommand("southwall", "", "", AllowedCaller.Both)] //Sets the south wall parameter of the arena wall
        [RocketCommandAlias("sw")]
        public void ExecuteCommandMinZ(IRocketPlayer caller, string[] parameters)
        {
            if (DisableGungame)
            {
                UnturnedChat.Say(caller, DefaultTranslations.Translate("GUNGAME_DISABLED"), Color.yellow);
                return;
            }

            try
            {
                int Num = Convert.ToInt32(parameters[0]);
                if (Num >= NorthWall)
                {
                    UnturnedChat.Say(caller, "South wall must be smaller than North wall!");
                }
                else
                {
                    SouthWall = Num;
                    saveXML();
                    UnturnedChat.Say(caller, "South wall set to: " + Num.ToString());
                    RebuildWall();
                }
            }
            catch
            {
                UnturnedChat.Say(caller, "Invalid parameters!");
            }
        }

        #endregion

        [RocketCommand("op", "", "", AllowedCaller.Player)] //makes the caller have the OP status in game
        public void ExecuteCommandOP(IRocketPlayer caller, string[] parameters)
        {
            if (DisableGungame)
            {
                UnturnedChat.Say(caller, DefaultTranslations.Translate("GUNGAME_DISABLED"), Color.yellow);
                return;
            }

            if (!OP.ContainsKey(((UnturnedPlayer)caller).CSteamID.m_SteamID))
            {
                OP.Add(((UnturnedPlayer)caller).CSteamID.m_SteamID, DateTime.Now.AddSeconds(20));
            }
        }

        [RocketCommand("lvld", "Removes a level", "", AllowedCaller.Player)] //decreases gun game level of caller
        public void ExecuteCommandL(IRocketPlayer caller, string[] parameters)
        {
            if (DisableGungame)
            {
                UnturnedChat.Say(caller, DefaultTranslations.Translate("GUNGAME_DISABLED"), Color.yellow);
                return;
            }

            UnturnedPlayer player = (UnturnedPlayer)caller;
            int pastLevel = 0;
            try
            {
                pastLevel = gungameLevels[player.CSteamID.m_SteamID];
            }
            catch { }
            gungameLevels.Remove(player.CSteamID.m_SteamID);
            if (pastLevel != 0)
                gungameLevels.Add(player.CSteamID.m_SteamID, pastLevel - 1);
        }

        [RocketCommand("lvlu", "Gives a level.", "", AllowedCaller.Player)] //increases gun game level of caller
        public void ExecuteuommandK(IRocketPlayer caller, string[] parameters)
        {
            if (DisableGungame)
            {
                UnturnedChat.Say(caller, DefaultTranslations.Translate("GUNGAME_DISABLED"), Color.yellow);
                return;
            }

            UnturnedPlayer player = (UnturnedPlayer)caller;
            int pastLevel = 0;
            try
            {
                pastLevel = gungameLevels[player.CSteamID.m_SteamID];
            }
            catch { }
            gungameLevels.Remove(player.CSteamID.m_SteamID);
            gungameLevels.Add(player.CSteamID.m_SteamID, pastLevel + 1);
        }

        [RocketCommand("reset", "Reset the gungame server", "", AllowedCaller.Both)] //resets the gun game
        public void ExecuteCommandReset(IRocketPlayer caller, string[] parameters)
        {
            if (DisableGungame)
            {
                UnturnedChat.Say(caller, DefaultTranslations.Translate("GUNGAME_DISABLED"), Color.yellow);
                return;
            }

            ResetGame();
        }

        [RocketCommand("unstick", "Respawns a player if they are stuck", "", AllowedCaller.Player)] //kills player if they are stuck somewhere
        public void ExecuteCommandUnstick(IRocketPlayer caller, string[] parameters)
        {
            if (DisableGungame)
            {
                UnturnedChat.Say(caller, DefaultTranslations.Translate("GUNGAME_DISABLED"), Color.yellow);
                return;
            }

            if (!disableUnstick)
            {
                UnturnedPlayer player = (UnturnedPlayer)caller;
                if (!UnstickCooldownList.ContainsKey(player.CSteamID.m_SteamID))
                {
                    UnstickList.Add(player.CSteamID.m_SteamID, DateTime.Now.AddSeconds(UnstickDelay));
                    UnstickCooldownList.Add(player.CSteamID.m_SteamID, DateTime.Now.AddSeconds(UnstickCooldown));
                    UnturnedChat.Say(caller, DefaultTranslations.Translate("UNSTICKING", UnstickDelay), Color.green);
                }
                else
                {
                    UnturnedChat.Say(caller, DefaultTranslations.Translate("UNSTICK_COOLDOWN", Math.Round((UnstickCooldownList[player.CSteamID.m_SteamID] - DateTime.Now).TotalSeconds)), Color.yellow);
                }
            }
            else
            {
                UnturnedChat.Say(caller, DefaultTranslations.Translate("NO_UNSTICK"), Color.yellow);
            }
        }

        [RocketCommandAlias("as")]
        [RocketCommand("addspawn", "Adds a spawnponint to the list of custom points", "", AllowedCaller.Player)] //Adds a spawnponint to the list of custom points
        public void ExecuteCommandSpawnpoint(IRocketPlayer caller, string[] parameters)
        {
            if (DisableGungame)
            {
                UnturnedChat.Say(caller, DefaultTranslations.Translate("GUNGAME_DISABLED"), Color.yellow);
                return;
            }

            if (parameters.Length != 1)
            {
                UnturnedChat.Say(caller, DefaultTranslations.Translate("SPAWN_ADD_ERR_NAME"), Color.yellow);
            }
            else
            {
                if (CustomSpawns.ContainsKey(parameters[0]))
                {
                    UnturnedChat.Say(caller, DefaultTranslations.Translate("SPAWN_ADD_ERR_EXISTS"), Color.yellow);
                }
                else
                {
                    Vector3 p = ((UnturnedPlayer)caller).Position;
                    CustomSpawns.Add(parameters[0], p);
                    UnturnedChat.Say(caller, DefaultTranslations.Translate("SPAWN_ADD", p.x, p.y, p.z, parameters[0]));
                    saveXML();
                }
            }
        }

        [RocketCommandAlias("ds")]
        [RocketCommandAlias("delspawn")]
        [RocketCommand("deletespawn", "Removes a spawnponint from the list of custom points", "", AllowedCaller.Player)] //Removes a spawnponint from the list of custom points
        public void ExecuteCommandDelspawnpoint(IRocketPlayer caller, string[] parameters)
        {
            if (DisableGungame)
            {
                UnturnedChat.Say(caller, DefaultTranslations.Translate("GUNGAME_DISABLED"), Color.yellow);
                return;
            }

            if (parameters.Length != 1)
            {
                UnturnedChat.Say(caller, DefaultTranslations.Translate("SPAWN_DEL_ERR_NAME"), Color.yellow);
            }
            else
            {
                if (!CustomSpawns.ContainsKey(parameters[0]))
                {
                    UnturnedChat.Say(caller, DefaultTranslations.Translate("SPAWN_DEL_ERR_EXISTS"), Color.yellow);
                }
                else
                {
                    CustomSpawns.Remove(parameters[0]);
                    UnturnedChat.Say(caller, DefaultTranslations.Translate("SPAWN_DEL", parameters[0]));
                    saveXML();
                }
            }
        }

        [RocketCommandAlias("ggstart")]
        [RocketCommand("gungamestart", "Starts a non-dedicated Gun Game round", "", AllowedCaller.Both)] //Starts a non-dedicated Gun Game round
        public void ExecuteCommandGungamestart(IRocketPlayer caller, string[] parameters)
        {
            if (DedicatedMode)
            {
                UnturnedChat.Say(caller, DefaultTranslations.Translate("GUNGAME_DEDICATED"), Color.yellow);
                return;
            }

            if (!DisableGungame)
            {
                UnturnedChat.Say(caller, DefaultTranslations.Translate("GUNGAME_RUNNING"), Color.yellow);
                return;
            }

            foreach(SteamPlayer sp in Provider.clients)
            {
                try
                {
                    DisableGungame = false;
                    UnturnedPlayer player = UnturnedPlayer.FromSteamPlayer(sp);
                    player.GiveItem(new Item(121, true));
                    Items i = new Items(1);
                    i.addItem(0, 0, 0, new Item(121, true));
                    player.Inventory.replaceItems(1, i);
                    ResetGame();
                }
                catch(Exception ex)
                {
                    Logger.LogError("Error enforcing start task for: " + sp.playerID + " Error: " + ex.Message);
                }
            }
            
            
        }

        [RocketCommandAlias("ggstop")]
        [RocketCommand("gungamestop", "Stop a non-dedicated Gun Game round", "", AllowedCaller.Both)] //Stops a non-dedicated Gun Game round
        public void ExecuteCommandGungamestop(IRocketPlayer caller, string[] parameters)
        {
            if(DedicatedMode)
            {
                UnturnedChat.Say(caller, DefaultTranslations.Translate("GUNGAME_DEDICATED"), Color.yellow);
                return;
            }

            if (DisableGungame)
            {
                UnturnedChat.Say(caller, DefaultTranslations.Translate("GUNGAME_STOPPED"), Color.yellow);
                return;
            }

            DisableGungame = true;
            foreach(SteamPlayer sp in Provider.clients)
            {
                try
                {
                    UnturnedPlayer player = UnturnedPlayer.FromSteamPlayer(sp);
                    ClearInv(player);
                    ClearClothes(player);
                    player.Heal(100);
                    MinSkills(player);
                    player.Inventory.removeItem(0, 0);
                    player.Inventory.removeItem(1, 0);                  
                }
                catch(Exception ex)
                {
                    Logger.LogError("Error enforcing stop task for: " + sp.playerID + " Error: " + ex.Message);
                }
            }

            if (!disableClearing)
            {
                BarricadeManager.askClearAllBarricades();
                StructureManager.askClearAllStructures();
                Logger.LogWarning(DefaultTranslations.Translate("BARRIC_CLR"));
                Logger.LogWarning(DefaultTranslations.Translate("STRUCT_CLR"));
            }
            UnturnedChat.Say(DefaultTranslations.Translate("ROUND_END"), Color.magenta);
        }

        #endregion

    }

    static class RandomExtensions
    {
        public static void Shuffle<T>(this IList<T> list)
        {
            try
            {
                RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider();
                int n = list.Count;
                while (n > 1)
                {
                    byte[] box = new byte[1];
                    do provider.GetBytes(box);
                    while (!(box[0] < n * (Byte.MaxValue / n)));
                    int k = (box[0] % n);
                    n--;
                    T value = list[k];
                    list[k] = list[n];
                    list[n] = value;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error while shuffling array: " + ex.Message);
            }
        }
    }
}

