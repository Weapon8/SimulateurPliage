using System.Drawing;

namespace SimulateurPliage.Vues
{
    /// <summary>Palette TolTem, partagée par toutes les vues.</summary>
    public static class Theme
    {
        public static readonly Color Fond    = Color.FromArgb(20, 24, 31);
        public static readonly Color Panneau = Color.FromArgb(27, 32, 39);
        public static readonly Color Champ   = Color.FromArgb(38, 44, 53);
        public static readonly Color Bouton  = Color.FromArgb(43, 49, 59);
        public static readonly Color Texte   = Color.FromArgb(230, 235, 240);
        public static readonly Color Discret = Color.FromArgb(138, 149, 162);
        public static readonly Color Accent  = Color.FromArgb(255, 122, 26);
        public static readonly Color Bord    = Color.FromArgb(70, 78, 90);
        public static readonly Color Separateur = Color.FromArgb(46, 53, 63);
        public static readonly Color Grille  = Color.FromArgb(34, 40, 49);

        public static readonly Color Tole    = Color.FromArgb(63, 131, 235);   // pli direct
        public static readonly Color Reprise = Color.FromArgb(63, 185, 80);
        public static readonly Color Alerte  = Color.FromArgb(229, 83, 75);

        public static readonly Color Outil   = Color.FromArgb(96, 104, 116);
        public static readonly Color Matrice = Color.FromArgb(74, 82, 94);
        public static readonly Color Embase  = Color.FromArgb(58, 64, 74);
    }
}
