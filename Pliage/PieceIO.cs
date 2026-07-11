using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimulateurPliage.Pliage
{
    /// <summary>
    /// Sauvegarde / chargement d'une pièce (pans + séquence) en JSON.
    /// Fichier autonome, indépendant de l'atelier : on transporte un programme de
    /// pliage, pas l'outillage. Extension .plt.json. Enums écrits en clair (Haut/Bas).
    /// </summary>
    public static class PieceIO
    {
        public const int CURRENT_VERSION = 1;
        public const string Extension = "plt.json";
        public const string Filtre = "Pièce de pliage TolTem (*.plt.json)|*.plt.json|Tous les fichiers (*.*)|*.*";

        sealed class Fichier
        {
            public int Version { get; set; } = CURRENT_VERSION;
            public string Outil = "SimulateurPliage";
            public Piece Piece { get; set; }
        }

        static JsonSerializerOptions Options() => new()
        {
            WriteIndented = true,
            IncludeFields = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static void Sauver(Piece p, string chemin)
        {
            var f = new Fichier { Piece = p };
            File.WriteAllText(chemin, JsonSerializer.Serialize(f, Options()));
        }

        /// <summary>Charge une pièce ; lève une exception si le fichier est illisible.</summary>
        public static Piece Charger(string chemin)
        {
            var f = JsonSerializer.Deserialize<Fichier>(File.ReadAllText(chemin), Options());
            var p = f?.Piece ?? throw new InvalidDataException("Fichier pièce vide ou illisible.");

            // Robustesse : un fichier bricolé à la main peut avoir des pans manquants
            // ou une séquence qui déborde du nombre de plis.
            if (p.Segments == null || p.Segments.Count < 2)
                throw new InvalidDataException("Pièce sans pans exploitables.");
            p.Sequence ??= new();
            p.Sequence.RemoveAll(o => o == null || o.Bend < 0 || o.Bend >= p.NbPlis);
            p.NormaliserReprises();
            return p;
        }
    }
}
