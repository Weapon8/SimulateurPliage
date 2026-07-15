using System;
using System.Collections.Generic;

namespace SimulateurPliage.Materiel
{
    /// <summary>
    /// Poinçon défini par son contour exact, repère pointe : pointe = (0,0), +Y vers le haut.
    /// Le contour est FIGÉ (relevé vectoriel). Seule la hauteur est réglable : le fût droit
    /// s'étire au-dessus de YStretch, le bec et le col de cygne restent intacts.
    /// </summary>
    public sealed class Poincon
    {
        public string Nom = "Rolleri P.150.35.R2";
        public double Hauteur  = 150;   // hauteur totale (réglable) — P.150 monté sur la Loire Safe
        public double AngleDeg = 35;    // bec total : 10° avant + 25° arrière
        public double R        = 2.0;   // rayon de pointe R2 (Rolleri P.150.35.R2)
        public double CorpsLg  = 26;

        const double HRef     = 120.0;
        const double UtileRef = 90.0;
        const double YStretch = 60.0;

        public double HauteurUtile => UtileRef + (Hauteur - HRef);

        public List<double[]> ContourPts = new()
        {
            new[] {   -7.000,  120.000 },
            new[] {   11.000,  120.000 },
            new[] {   11.000,  105.000 },
            new[] {    7.000,  105.000 },
            new[] {    7.000,   94.000 },
            new[] {   11.000,   94.000 },
            new[] {   11.000,   90.000 },
            new[] {   11.000,   60.000 },
            new[] {    9.464,   56.786 },
            new[] {    8.079,   53.571 },
            new[] {    6.843,   50.357 },
            new[] {    5.757,   47.143 },
            new[] {    4.821,   43.929 },
            new[] {    4.036,   40.714 },
            new[] {    3.400,   37.500 },
            new[] {    2.914,   34.286 },
            new[] {    2.579,   31.071 },
            new[] {    2.393,   27.857 },
            new[] {    2.357,   24.643 },
            new[] {    2.471,   21.429 },
            new[] {    2.736,   18.214 },
            new[] {    3.150,   15.000 },
            new[] {    0.521,    0.046 },
            new[] {    0.435,    0.021 },
            new[] {    0.347,    0.005 },
            new[] {    0.259,   -0.002 },
            new[] {    0.170,   -0.001 },
            new[] {    0.081,    0.007 },
            new[] {   -0.008,    0.023 },
            new[] {   -0.096,    0.047 },
            new[] {   -0.183,    0.078 },
            new[] {   -0.267,    0.117 },
            new[] {   -0.349,    0.163 },
            new[] {   -0.427,    0.216 },
            new[] {   -0.501,    0.276 },
            new[] {   -1.268,    0.281 },
            new[] {   -8.127,   15.000 },
            new[] {   -8.356,   15.000 },
            new[] {   -8.814,   18.214 },
            new[] {   -9.273,   21.429 },
            new[] {   -9.731,   24.643 },
            new[] {  -10.189,   27.857 },
            new[] {  -10.647,   31.071 },
            new[] {  -11.105,   34.286 },
            new[] {  -11.564,   37.500 },
            new[] {  -12.022,   40.714 },
            new[] {  -12.480,   43.929 },
            new[] {  -12.938,   47.143 },
            new[] {  -13.396,   50.357 },
            new[] {  -13.854,   53.571 },
            new[] {  -14.313,   56.786 },
            new[] {  -14.771,   60.000 },
            new[] {  -15.000,   90.000 },
        };

        /// <summary>
        /// Contour ferme, etire a la hauteur courante. AUCUN miroir.
        /// Vue : butee a droite, operateur a gauche.
        /// Le flanc ETROIT (x jusqu'a +11) est le degagement du col de cygne : il est en
        /// retrait et regarde deja la butee, c'est lui qui recoit le retour deja plie.
        /// Le flanc large (x jusqu'a -15) est le corps deporte, cote operateur.
        /// Valide sur le chevetre 20/60/100/60/20 et le fourreau a crosse 100/150/30/20.
        /// </summary>
        public List<double[]> Contour()
        {
            double d = Hauteur - HRef;
            var c = new List<double[]>(ContourPts.Count);
            foreach (var p in ContourPts)
                c.Add(new[] { p[0], p[1] >= YStretch ? p[1] + d : p[1] });
            return c;
        }

        /// <summary>Demi-largeur (max |x|) à la hauteur y, sur le contour étiré.</summary>
        public double DemiLargeur(double y)
        {
            var c = Contour();
            double best = 0.2; bool found = false;
            for (int i = 0; i < c.Count; i++)
            {
                var p1 = c[i]; var p2 = c[(i + 1) % c.Count];
                double y1 = p1[1], y2 = p2[1];
                if ((y1 <= y && y <= y2) || (y2 <= y && y <= y1))
                {
                    double t = Math.Abs(y2 - y1) > 1e-9 ? (y - y1) / (y2 - y1) : 0;
                    best = Math.Max(best, Math.Abs(p1[0] + (p2[0] - p1[0]) * t));
                    found = true;
                }
            }
            return found ? best : 0.2;
        }

        /// <summary>Point à l'intérieur du contour (ray casting).</summary>
        public bool Contient(double x, double y)
        {
            var c = Contour();
            bool inside = false;
            for (int i = 0, j = c.Count - 1; i < c.Count; j = i++)
            {
                double xi = c[i][0], yi = c[i][1], xj = c[j][0], yj = c[j][1];
                if (((yi > y) != (yj > y)) && (x < (xj - xi) * (y - yi) / (yj - yi + 1e-12) + xi))
                    inside = !inside;
            }
            return inside;
        }

        public override string ToString() => Nom;
    }
}
