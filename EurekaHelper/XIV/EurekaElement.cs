namespace EurekaHelper.XIV
{
    public enum EurekaElement
    {
        Wind,
        Water,
        Earth,
        Lightning,
        Fire,
        Ice,
        Unknown
    }

    public static class EurekaElementExtensions
    {
        public static string ToFriendlyString(this EurekaElement element)
        {
            return element switch
            {
                EurekaElement.Wind => Loc.Text("Wind"),
                EurekaElement.Water => Loc.Text("Water"),
                EurekaElement.Earth => Loc.Text("Earth"),
                EurekaElement.Lightning => Loc.Text("Lightning"),
                EurekaElement.Fire => Loc.Text("Fire"),
                EurekaElement.Ice => Loc.Text("Ice"),
                _ => Loc.Text("Unknown")
            };
        }
    }
}
