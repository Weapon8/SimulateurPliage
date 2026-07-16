using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SimulateurPliage.Pliage
{
    /// <summary>
    /// Pont vers l'outil PareGravier de la suite TolTem. Il calcule DÉJÀ le développé, les
    /// languettes, les oblongs et le DXF — on ne refait pas son boulot. Ce qu'il n'a pas :
    /// son ordre de pliage est une heuristique en dur (Geometry.OrderIndices : « retombées
    /// d'abord, puis côtés, puis languettes ») qui ne vérifie NI les collisions, NI ce qui
    /// reste en main à l'opérateur.
    ///
    /// C'est ce trou-là qu'on remplit : on reprend ses paramètres, on en sort les deux axes,
    /// et on les passe au vrai moteur.
    ///
    /// Structure PareGravier (Geometry.Build) : A = H + R, le développé va de -A à L+A.
    /// La section d'un axe est donc  retombée R · paroi H · dessus L · paroi H · retombée R
    /// — la topologie du chevêtre, que le moteur 1D traite déjà.
    ///
    /// Les deux axes sont indépendants : les parois relevées de l'un sortent du plan de
    /// section de l'autre, et l'outillage peut être segmenté (Weapon).
    /// Les LANGUETTES ne sont pas des plis : elles sont solidaires du rabat et se plient
    /// avec lui — c'est écrit dans son Geometry.cs, on ne les compte pas.
    /// </summary>
    public sealed class Boite
    {
        // ---- mêmes noms et mêmes défauts que PareGravier.PareParams ----
        public double L = 250;                 // dessus, sens X
        public double l = 250;                 // dessus, sens Y
        public double H = 70;                  // hauteur de paroi
        public double R = 20;                  // retombée / rabat de fixation
        public double E = 1.5;                 // épaisseur
        public double FoldAngle = 92;          // les 4 parois
        public double RetombeeAngle = 90;      // les 4 retombées
        public double V = 16;

        public string Nom => $"Pare-gravier {L:0}×{l:0} H{H:0} ret{R:0}";

        /// <summary>Encombrement du flan, comme Geometry.Build.</summary>
        public (double x, double y) Flan() => (L + 2 * (H + R), l + 2 * (H + R));

        /// <summary>
        /// Un axe en bande 1D : retombée · paroi · dessus · paroi · retombée.
        /// Séquence dans l'ordre de PareGravier ET de l'atelier : les retombées d'abord,
        /// sur le flan à plat — puis on retourne, et les parois qui referment la boîte.
        /// </summary>
        public Piece Axe(bool axeX)
        {
            double dessus = axeX ? L : l;
            var p = new Piece
            {
                Nom = Nom + (axeX ? " · axe X" : " · axe Y"),
                Epaisseur = E,
                LongueurPli = axeX ? l : L      // le pli d'un axe court sur le dessus de l'autre
            };
            p.Segments.AddRange(new[] { R, H, dessus, H, R });

            p.Sequence.Add(new Operation { Bend = 0, AngleCible = RetombeeAngle, Sens = Sens.Haut, V = V });
            p.Sequence.Add(new Operation { Bend = 3, AngleCible = RetombeeAngle, Sens = Sens.Haut, V = V, ButeeAval = true });
            p.Sequence.Add(new Operation { Bend = 1, AngleCible = FoldAngle, Sens = Sens.Haut, V = V, Retournee = true });
            p.Sequence.Add(new Operation { Bend = 2, AngleCible = FoldAngle, Sens = Sens.Haut, V = V, Retournee = true, ButeeAval = true });
            p.AssurerForme();
            return p;
        }

        /// <summary>Les deux axes : huit plis en tout, quatre par axe.</summary>
        public List<Piece> Axes() => new() { Axe(true), Axe(false) };

        /// <summary>
        /// Lit le paregravier.settings.json écrit par l'outil PareGravier (Prima EX5).
        /// On débite sur la Prima, on plie sur la Loire Safe — les deux doivent parler des
        /// MÊMES cotes. Retaper les chiffres à la main, c'est une faute qui attend son tour.
        /// Les champs inconnus (languettes, oblongs, faces) sont ignorés : ils regardent le
        /// débit, pas le pliage.
        /// </summary>
        public static Boite Importer(string chemin, out string erreur)
        {
            erreur = null;
            try
            {
                if (!File.Exists(chemin)) { erreur = "fichier introuvable"; return null; }
                using var doc = JsonDocument.Parse(File.ReadAllText(chemin));
                var r = doc.RootElement;
                double D(string n, double def)
                    => r.TryGetProperty(n, out var v) && v.TryGetDouble(out double d) ? d : def;

                var b = new Boite
                {
                    L = D("L", 250), l = D("l", 250), H = D("H", 70), R = D("R", 20), E = D("E", 1.5),
                    FoldAngle = D("FoldAngle", 92), RetombeeAngle = D("RetombeeAngle", 90)
                };
                if (b.L <= 0 || b.l <= 0 || b.H <= 0 || b.R <= 0 || b.E <= 0)
                { erreur = "cotes invalides dans le fichier"; return null; }
                return b;
            }
            catch (Exception ex) { erreur = ex.Message; return null; }
        }

        /// <summary>Chemin par défaut : PareGravier écrit à côté de son exe.</summary>
        public static string CheminDefaut(string dossierPareGravier)
            => Path.Combine(dossierPareGravier ?? "", "paregravier.settings.json");
    }
}
