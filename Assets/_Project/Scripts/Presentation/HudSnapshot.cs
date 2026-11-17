namespace LyokhaVR
{
    /// <summary>Presentation data only; the session remains the authority for gameplay.</summary>
    [System.Serializable]
    public sealed class HudSnapshot
    {
        public string title = "ЛЁХА / ДВОРЫ";
        public string stats = "";
        public string objective = "Собрать 250 рублей и купить еды.";
        public string prompt = "";
        public string message = "";
        public string[] inventory = new string[0];
        public int selectedIndex;
        public bool night, showMenu, showInventory, showHelp, paused, flashlightOn;
        public string endingTitle = "", endingBody = "", decisionTitle = "", decisionText = "";
        public string[] decisionOptions = new string[0];
        public float health01 = 1f, battery01 = 1f, clock01;
    }
}
