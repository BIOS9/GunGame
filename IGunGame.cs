using Rocket.API;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;

namespace NightFish.GunGame
{
    public interface IGunGame
    {
        void ClearClothes(UnturnedPlayer player);
        void ClearInv(UnturnedPlayer player);
        void ExecuteCommandAmmo(IRocketPlayer caller, string[] parameters);
        void ExecuteCommandMaxx(IRocketPlayer caller, string[] parameters);
        void ExecuteCommandMaxz(IRocketPlayer caller, string[] parameters);
        void ExecuteCommandMinx(IRocketPlayer caller, string[] parameters);
        void ExecuteCommandMinZ(IRocketPlayer caller, string[] parameters);
        void ExecuteCommandOP(IRocketPlayer caller, string[] parameters);
        void ExecuteCommandReset(IRocketPlayer caller, string[] parameters);
        void GenerateWeaponList();
        void MaxSkills(UnturnedPlayer player);
        void MinSkills(UnturnedPlayer player);
        void PlayerDeath(UnturnedPlayer player, EDeathCause cause, ELimb limb, CSteamID murderer);
        void ResetGame();
    }
}