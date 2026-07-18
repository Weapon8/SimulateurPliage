using System;
using System.Collections.Generic;

namespace SimulateurPliage.Pliage
{
    /// <summary>
    /// Détection des collisions entre la pièce et l'outillage, à une étape donnée.
    /// La tôle qui longe l'âme du poinçon n'est PAS une collision : elle se forme
    /// autour de l'outil. Seuls les retours qui reviennent par le flanc comptent.
    /// </summary>
    public static class Detecteur
    {
        /// <summary>
        /// Tolérance sur les COTES DE PAN. Weapon programme rond : il tape 10 là où le pan
        /// fait 10,2 en vrai. Sans elle, la butée mini (10,2) ET l'aile mini (0,63 × 16 =
        /// 10,08) rejettent toutes les deux un pan de 10 — donc le Z laqué et la couvertine,
        /// deux pièces réellement produites. Même cause, même tolérance : ce n'est pas un
        /// rustine sur la machine, c'est l'écart entre la cote programmée et la cote réelle.
        /// </summary>
        public const double TolButee = 0.5;

        /// <summary>
        /// Effort de pliage EN L'AIR. Formule classique : F [kN/m] = 1,42 × Rm × ep² / V,
        /// puis /9,81 pour passer en t/m. Renvoie le total en tonnes sur la longueur de pli.
        ///
        /// L'effort est en 1/V : c'est TOUTE la raison d'être d'une matrice 4 voies. Sur du
        /// fort on ouvre le vé, le tonnage s'effondre, et on n'écrase ni la machine ni l'outil.
        /// Du 4 mm en V16 sur 4 m demande ~260 t ; le même pli en V50 en demande ~83.
        /// </summary>
        public static double Tonnage(double rm, double ep, double v, double longueurPli, out double tParMetre)
        {
            tParMetre = 0;
            if (v <= 0 || ep <= 0 || rm <= 0) return 0;
            tParMetre = (1.42 * rm * ep * ep / v) / 9.81;      // kN/m -> t/m
            return tParMetre * Math.Max(0, longueurPli) / 1000.0;
        }

        public static List<Collision> Analyser(EtatEtape st, Piece p, Materiel.Plieuse plieuse,
                                               Materiel.Poincon poincon, Materiel.Matrice matrice, Materiel.Embase embase)
        {
            var res = new List<Collision>();
            if (st.Op == null) return res;

            double ep = Math.Max(0.2, p.Epaisseur);

            // Le poinçon est remonté de ep : sa pointe touche la face haute de la tôle,
            // sinon le pan posé à plat (y = 0) raserait la pointe.
            List<double[]> poinconLev = null;
            if (poincon != null)
            {
                poinconLev = new List<double[]>();
                foreach (var q in poincon.Contour()) poinconLev.Add(new[] { q[0], q[1] + ep / 2.0 });
            }

            var vf = matrice?.VProche(st.Op.V);
            double vOuv = vf?.V ?? st.Op.V;
            var matriceC = matrice?.Contour(st.Op.V);
            double dieBas = matriceC != null ? Min(matriceC, 1) : -30;

            List<double[]> porteP = null, semelle = null;
            if (embase != null)
            {
                double haut = (poincon?.Hauteur ?? 120) + ep;
                if (embase.PortePoinconLg > 0 && embase.PortePoinconH > 0)
                    porteP = Rect(-embase.PortePoinconLg / 2, haut, embase.PortePoinconLg / 2, haut + embase.PortePoinconH);
                if (embase.SemelleLg > 0 && embase.SemelleH > 0)
                    semelle = Rect(-embase.SemelleLg / 2, dieBas - embase.SemelleH, embase.SemelleLg / 2, dieBas);
            }



            // Zone morte : autour de la pointe, dans le vé, la tôle est en cours de formage.
            double zoneX = vOuv / 2.0 + ep + 4;
            double zoneY = ep + 6;
            bool EnFormage(Pt q) => q.Y <= zoneY && q.Y >= -2 && Math.Abs(q.X) <= zoneX;

            // Dans l'âme du poinçon : la tôle épouse l'outil, pas une collision.
            bool DansAme(Pt q) => poincon != null && poincon.Contient(q.X, q.Y - ep / 2.0);

            var segments = Segments(st);

            // Autour du sommet, la tole s'enroule sur la pointe : tout croisement dans ce
            // rayon est du formage, pas une collision. (Sommet vif cote tole, pointe ronde
            // cote outil : sans ca, chaque pli sort un faux positif.)
            double rayonMort = ep + (poincon?.R ?? 1.0) + 2.0;

            bool hitP = false, hitM = false, hitPP = false, hitSem = false;
            foreach (var (a, b) in segments)
            {
                if (EnFormage(a) && EnFormage(b)) continue;

                if (!hitP && poinconLev != null && !(DansAme(a) && DansAme(b))
                    && CroiseHorsPointe(a, b, poinconLev, rayonMort))
                    hitP = true;
                if (!hitM && matriceC != null && (a.Y < -0.6 || b.Y < -0.6) && Croise(a, b, matriceC))
                    hitM = true;
                if (!hitPP && porteP != null && Croise(a, b, porteP)) hitPP = true;
                if (!hitSem && semelle != null && Croise(a, b, semelle)) hitSem = true;
            }

            if (hitP) res.Add(new Collision("poinçon", "un retour de tôle tape le poinçon", true));
            if (hitM) res.Add(new Collision("matrice", "la tôle tape la matrice", true));
            if (hitPP) res.Add(new Collision("porte-poinçon", "un retour touche l'embase du poinçon", true));
            if (hitSem) res.Add(new Collision("semelle", "la pièce touche la semelle", true));
            // TABLIER — collision par HAUTEUR VERTICALE VRAIE des ailes déjà formées.
            // On ne peut PAS tester le tablier dans le repère bissectrice (il pivote avec le
            // pli, une aile verticale y apparaît à 45°) : on calcule la vraie élévation de
            // chaque pan au-dessus de la face matrice, monde vertical. Un pan de longueur L
            // relevé d'un angle de pli 'a' (180=plat) monte de L·sin(180−a). Marge de flexion :
            // le tablier fléchit de 1–2 mm sous charge (montants), donc on garde 5 mm de sûreté
            // pour ne pas valider une aile qui passe de justesse à l'écran mais tape en vrai.
            if (plieuse != null && plieuse.TablierHauteur > 0)
            {
                double garde = plieuse.TablierHauteur - 5.0;   // marge de flexion
                double hmax = HauteurAilesFormees(st, p);
                if (hmax > garde)
                    res.Add(new Collision("tablier",
                        $"une aile formée monte à {hmax:0} mm et tape le tablier "
                        + $"(garde {plieuse.TablierHauteur:0} mm)", true));
            }

            if (ReplieSurElleMeme(st))
                res.Add(new Collision("repli sur repli", "la pièce se referme sur elle-même", true));

            if (plieuse != null && st.ButeeDistance > plieuse.ButeeMax)
                res.Add(new Collision("butée arrière",
                    $"pan de {st.ButeeDistance:0} mm > course butée {plieuse.ButeeMax:0} mm", false));

            // Butée mini : la cote machine (10,2) est un relevé au réglet, pas une loi. Un pan
            // de 10 se cale en vrai, même en 4 mm — Weapon. On tolère 0,5 mm pour ne pas sortir
            // un faux positif sur une cote ronde. En dessous, l'alerte est légitime.
            // PLANCHER D'ANGLE. En pliage en l'air on ne descend pas sous l'angle du VÉ : le
            // poinçon bute au fond avant. Ni sous l'angle du BEC : les ailes de la tôle
            // taperaient ses flancs. Le plancher, c'est le PLUS GRAND des deux.
            // C'est parce que le Rolleri a un bec à 35° qu'il rentre dans un vé à 45° et sort
            // le pli du Z. Le même 45° est impossible sur la 4 voies dont le V16 est à 88°.
            // Sous le plancher, ce n'est plus du pliage en l'air : c'est un écrasement, en deux
            // opérations avec un autre outil. Le simulateur ne doit pas laisser croire que ça
            // passe d'un coup — il envoyait « propre » sur un pli à 10° dans un vé à 45°.
            double angleVe = vf?.AngleDeg ?? 0;
            double angleBec = poincon?.AngleDeg ?? 0;
            double plancher = Math.Max(angleVe, angleBec);
            if (plancher > 0 && st.Op.AngleCible < plancher - 0.01)
                res.Add(new Collision("angle impossible",
                    $"{st.Op.AngleCible:0.#}° visé < plancher {plancher:0.#}° "
                    + (angleVe >= angleBec ? $"(vé à {angleVe:0.#}°)" : $"(bec à {angleBec:0.#}°)")
                    + " — en l'air on ne descend pas plus bas", true));

            // Tonnage : la machine, puis l'outil. Dans les deux cas la sortie est la même —
            // OUVRIR LE VÉ. C'est le geste qui sauve la presse et le poinçon.
            double tpm;
            double tonnes = Tonnage(p.Rm, ep, vOuv, p.LongueurPli, out tpm);
            if (plieuse != null && plieuse.TonnageMax > 0 && tonnes > plieuse.TonnageMax)
                res.Add(new Collision("tonnage machine",
                    $"{tonnes:0} t nécessaires > {plieuse.TonnageMax:0} t machine — ouvre le vé", true));
            if (poincon != null && poincon.TonnageParMetre > 0 && tpm > poincon.TonnageParMetre)
                res.Add(new Collision("tonnage poinçon",
                    $"{tpm:0} t/m > {poincon.TonnageParMetre:0} t/m admissible — tu vas marquer l'outil", true));

            if (plieuse != null && st.ButeeDistance > 0
                && st.ButeeDistance < plieuse.ButeeMin - TolButee)
                res.Add(new Collision("butée arrière",
                    $"pan de {st.ButeeDistance:0} mm < butée mini {plieuse.ButeeMin:0.#} mm", false));

            // AILE MINI. En pliage en l'air, une aile qui n'atteint plus les épaulements du vé
            // bascule DEDANS au lieu de se relever. Le plancher est le PLUS GRAND de deux
            // contraintes qui n'ont rien à voir :
            //   - le vé          0,63 × V  — géométrique, mord sur les GROS vés (V50 -> 31,5)
            //   - la butée mini  10,2      — machine, mord sur les PETITS (V16 -> 0,63×V = 10,08)
            // En V16 c'est la butée qui limite, pas le vé : à 0,08 mm près ils disent pareil,
            // et c'est une coïncidence — il ne faut pas en déduire que l'un remplace l'autre.
            //
            // On teste les DEUX pans qui bordent le pli. Le contrôle de butée ci-dessus ne
            // regarde que le pan couché contre les doigts ; celui du CÔTÉ FORMAGE n'était vu
            // par personne. Un 4 mm côté opérateur passait « propre ».
            var bandeA = p.Bande(st.Op.Axe);
            double plancherAile = Math.Max(0.63 * vOuv, plieuse?.ButeeMin ?? 0);
            if (plancherAile > 0 && st.Op.Bend >= 0 && st.Op.Bend + 1 < bandeA.Segments.Count)
            {
                double a1 = bandeA.ButeeInt(st.Op.Bend);
                double a2 = bandeA.ButeeInt(st.Op.Bend + 1);
                double aile = Math.Min(a1, a2);
                if (aile > 0 && aile < plancherAile - TolButee)
                    res.Add(new Collision("aile mini",
                        $"aile de {aile:0.#} mm < mini {plancherAile:0.#} mm "
                        + (0.63 * vOuv >= (plieuse?.ButeeMin ?? 0)
                             ? $"(0,63 × V{vOuv:0})" : $"(butée mini {plieuse.ButeeMin:0.#})")
                        + " — elle bascule dans le vé", true));
            }

            // LONGUEUR DE PLI. Cotes relevées sur machine et présentes dans le preset depuis
            // le début, mais que personne ne relisait : un pli de 5000 sur une machine de 4050
            // sortait « propre ».
            if (plieuse != null && p.LongueurPli > 0)
            {
                if (plieuse.LongPliMax > 0 && p.LongueurPli > plieuse.LongPliMax)
                    res.Add(new Collision("longueur de pli",
                        $"{p.LongueurPli:0} mm > {plieuse.LongPliMax:0} mm admissibles sur {plieuse.Nom}", true));
                else if (plieuse.LongPliMin > 0 && p.LongueurPli < plieuse.LongPliMin)
                    res.Add(new Collision("longueur de pli",
                        $"{p.LongueurPli:0} mm < {plieuse.LongPliMin:0} mm — pli trop court pour la machine", false));
            }

            return res;
        }

        /// <summary>
        /// Segments à tester = les RETOURS DÉJÀ PLIÉS, uniquement. De chaque côté du sommet
        /// actif on saute la partie rectiligne du bras en cours de formage : elle épouse la
        /// pointe, ce n'est jamais une collision. Un « retour » n'existe qu'AU-DELÀ du premier
        /// coude. Conséquence directe : au tout premier pli il n'y a aucun coude antérieur,
        /// donc aucun segment testé — rien ne peut « taper le poinçon », ce qui est la réalité
        /// (un bec à 35° laisse le champ libre à un pli de 45°).
        /// </summary>
        static List<(Pt, Pt)> Segments(EtatEtape st)
        {
            var segs = new List<(Pt, Pt)>();
            ApresMontant(segs, st.PanArriere, sommetEnFin: true);   // pan couché : sommet = dernier point
            ApresMontant(segs, st.Formage,    sommetEnFin: false);  // formage    : sommet = premier point
            return segs;
        }

        /// <summary>Ajoute les segments d'un pan en partant DU SOMMET vers l'extérieur, en
        /// sautant le montant rectiligne initial (le bras droit, non plié, du pli actif).</summary>
        static void ApresMontant(List<(Pt, Pt)> segs, List<Pt> pan, bool sommetEnFin)
        {
            int n = pan.Count;
            if (n < 2) return;
            var ordre = new int[n];
            for (int k = 0; k < n; k++) ordre[k] = sommetEnFin ? n - 1 - k : k;

            Pt d0 = Unitaire(pan[ordre[1]].X - pan[ordre[0]].X, pan[ordre[1]].Y - pan[ordre[0]].Y);
            bool montant = true;
            for (int k = 0; k + 1 < n; k++)
            {
                int a = ordre[k], b = ordre[k + 1];
                Pt d = Unitaire(pan[b].X - pan[a].X, pan[b].Y - pan[a].Y);
                if (montant && d.X * d0.X + d.Y * d0.Y > 0.985) continue;   // encore le bras droit
                montant = false;
                segs.Add((pan[a], pan[b]));
            }
        }

        static bool ReplieSurElleMeme(EtatEtape st)
        {
            var poly = new List<Pt>(st.PanArriere);
            for (int i = 1; i < st.Formage.Count; i++) poly.Add(st.Formage[i]);

            for (int i = 0; i + 1 < poly.Count; i++)
                for (int j = i + 2; j + 1 < poly.Count; j++)
                {
                    if (i == 0 && j == poly.Count - 2) continue;
                    if (Intersecte(poly[i], poly[i + 1], poly[j], poly[j + 1])) return true;
                }
            return false;
        }

        // ---- primitives géométriques ----

        static List<double[]> Rect(double x0, double y0, double x1, double y1) => new()
        {
            new[] { x0, y0 }, new[] { x1, y0 }, new[] { x1, y1 }, new[] { x0, y1 }
        };

        static double Min(List<double[]> poly, int axe)
        {
            double m = double.MaxValue;
            foreach (var p in poly) m = Math.Min(m, p[axe]);
            return m;
        }

        static bool Croise(Pt a, Pt b, List<double[]> poly)
        {
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                if (Intersecte(a, b, new Pt(poly[j][0], poly[j][1]), new Pt(poly[i][0], poly[i][1])))
                    return true;
            return false;
        }

        /// <summary>Croisement reel, en ignorant ceux qui tombent dans le rayon de formage
        /// autour du sommet du pli (origine du repere).</summary>
        static bool CroiseHorsPointe(Pt a, Pt b, List<double[]> poly, double rayonMort)
        {
            double r2 = rayonMort * rayonMort;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                var p3 = new Pt(poly[j][0], poly[j][1]);
                var p4 = new Pt(poly[i][0], poly[i][1]);
                if (!Intersecte(a, b, p3, p4)) continue;
                if (Point(a, b, p3, p4, out var ip) && ip.X * ip.X + ip.Y * ip.Y <= r2) continue;
                return true;
            }
            return false;
        }

        static bool Point(Pt p1, Pt p2, Pt p3, Pt p4, out Pt ip)
        {
            ip = default;
            double d = (p2.X - p1.X) * (p4.Y - p3.Y) - (p2.Y - p1.Y) * (p4.X - p3.X);
            if (Math.Abs(d) < 1e-12) return false;
            double t = ((p3.X - p1.X) * (p4.Y - p3.Y) - (p3.Y - p1.Y) * (p4.X - p3.X)) / d;
            ip = new Pt(p1.X + t * (p2.X - p1.X), p1.Y + t * (p2.Y - p1.Y));
            return true;
        }

        static bool Intersecte(Pt p1, Pt p2, Pt p3, Pt p4)
        {
            double d1 = Cross(p3, p4, p1), d2 = Cross(p3, p4, p2);
            double d3 = Cross(p1, p2, p3), d4 = Cross(p1, p2, p4);
            const double e = 1e-7;   // colinéaire / tangent -> PAS un croisement (évite un faux « repli »)
            return ((d1 > e && d2 < -e) || (d1 < -e && d2 > e))
                && ((d3 > e && d4 < -e) || (d3 < -e && d4 > e));
        }

        static double Cross(Pt a, Pt b, Pt c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

        static Pt Unitaire(double x, double y)
        {
            double m = Math.Sqrt(x * x + y * y);
            return m > 1e-9 ? new Pt(x / m, y / m) : new Pt(0, 0);
        }
    
        /// <summary>
        /// Hauteur VERTICALE MAXI, au-dessus de la face matrice, atteinte par les ailes DÉJÀ
        /// FORMÉES à cette étape — dans le monde réel (aile verticale = pleine hauteur), pas
        /// dans le repère bissectrice du dessin. On parcourt les plis faits avant l'étape
        /// courante et on cumule l'élévation de chaque pan relevé. Un pan de longueur L relevé
        /// d'un angle de pli 'a' monte de L·sin(180−a) ; les pans à plat (a=180) montent de 0.
        /// C'est ce qui vient toucher le tablier sur un grand développé ou une aile de 300.
        /// </summary>
        static double HauteurAilesFormees(EtatEtape st, Piece p)
        {
            // angles réellement acquis à cette étape (plis d'index < étape, + le pli courant)
            int nb = p.NbPlis;
            var faitAngle = new double[nb];
            for (int i = 0; i < nb; i++) faitAngle[i] = 180.0;   // 180 = à plat = pas encore formé
            for (int s = 0; s <= st.Etape && s < p.Sequence.Count; s++)
            {
                var o = p.Sequence[s];
                if (o.Bend >= 0 && o.Bend < nb) faitAngle[o.Bend] = o.AngleCible;
            }
            // on suit la fibre : à partir de la ligne de pli active, on cumule l'élévation des
            // pans en remontant vers l'extérieur, tant que le pan précédent est relevé.
            var seg = p.Segments;
            double hmax = 0, h = 0;
            // côté opérateur (aval du sommet) puis côté butée (amont) : on prend le plus haut.
            for (int dir = 0; dir < 2; dir++)
            {
                h = 0;
                int bend = st.Op.Bend;
                if (dir == 0) // vers l'aval : plis bend, bend+1, ...
                    for (int b = bend; b < nb; b++)
                    {
                        double relev = (180.0 - faitAngle[b]) * Math.PI / 180.0;
                        h += (b + 1 < seg.Count ? seg[b + 1] : 0) * Math.Abs(Math.Sin(relev));
                        if (h > hmax) hmax = h;
                        if (Math.Abs(faitAngle[b] - 180.0) < 1) break;   // pan à plat : on ne monte plus
                    }
                else          // vers l'amont : plis bend, bend-1, ...
                    for (int b = bend; b >= 0; b--)
                    {
                        double relev = (180.0 - faitAngle[b]) * Math.PI / 180.0;
                        h += (b < seg.Count ? seg[b] : 0) * Math.Abs(Math.Sin(relev));
                        if (h > hmax) hmax = h;
                        if (Math.Abs(faitAngle[b] - 180.0) < 1) break;
                    }
            }
            return hmax;
        }

}
}
