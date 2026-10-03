using System;
using System.IO;
using System.Windows.Forms;
using SimulateurPliage.Vues;

namespace SimulateurPliage
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Une erreur imprévue dans l'interface ne doit ni fermer l'appli sur l'opérateur,
            // ni lui sortir la boîte grise de WinForms : on dit ce qui s'est passé en clair,
            // on le note dans un journal, et on continue. À poser AVANT toute fenêtre.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => Signaler(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Signaler(e.ExceptionObject as Exception);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.Run(new FenetrePrincipale());
        }

        /// <summary>Journal des erreurs : %LocalAppData%\TolTem\SimulateurPliage\erreurs.log</summary>
        static string Journal()
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TolTem", "SimulateurPliage");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "erreurs.log");
            }
            catch { return null; }
        }

        static void Signaler(Exception ex)
        {
            if (ex == null) return;
            string journal = Journal();
            try
            {
                if (journal != null)
                    File.AppendAllText(journal,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + ex + Environment.NewLine + Environment.NewLine);
            }
            catch { }

            try
            {
                MessageBox.Show(
                    "Une erreur est survenue, l'appli continue.\n\n" + ex.Message
                    + (journal != null ? "\n\nDétail noté dans :\n" + journal : ""),
                    "Simulateur de pliage", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch { }
        }
    }
}
