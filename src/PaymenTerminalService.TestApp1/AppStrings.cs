using System.Reflection;

namespace PaymentTerminalService.TestApp1
{
    public static class AppStrings
    {
        public static string AppTitle => "PaymentTerminalService Test App";
        public static string AppVersion => $"v{Assembly.GetExecutingAssembly().GetName().Version.ToString()}";
        public static string Unknown => "Unknown";
        public static string NA => "NA";

        //todo: localize
        public static string LoadTerminals => "Load terminals";
        public static string SelectTerminal => "Select terminal";
        public static string GetSettings => "Get settings";
        public static string GetStatus => "Get status";
        public static string GetSession => "Get session";
        public static string GetTerminalStatus => "Get terminal status";
        public static string Abort => "Abort";
        public static string AbortForce => "Abort force";
        public static string Purchase => "Purchase";
        public static string Reversal => "Reversal";
        public static string Refund => "Refund";
        public static string Release => "Release";
        public static string LoyaltyActivate => "Loyalty start";
        public static string LoyaltyDeactivate => "Loyalty stop";
    }
}
