using System;
using System.Windows.Forms;
using SimulateurPliage.Vues;

namespace SimulateurPliage
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.Run(new FenetrePrincipale());
        }
    }
}
