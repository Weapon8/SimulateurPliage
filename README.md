# Simulateur de pliage — collisions outillage

**WinForms .NET 8** (thème TolTem). Visualise une **séquence de pliage** en section 2D, étape par
étape, **détecte les collisions** pièce ↔ outillage — et **trouve l'ordre de pliage tout seul**.

Fait pour la plieuse **Loire Safe 4 m**, poinçon **Rolleri P.150.35.R2**, matrices Euram.
Toutes les cotes machine et outillage sont ajustables dans l'app.

> Le but n'est pas de remplacer le plieur. C'est de donner à un intérimaire, un apprenti ou un
> chef en reconversion l'ordre de pliage qu'un gars de 26 ans de métier trouve d'instinct —
> et de le prévenir quand il va avoir les doigts près du bec.

---

## Ce que ça fait

**Saisie** — nombre de plis, épaisseur, longueur des pans, cotes intérieures ou extérieures.

**Séquence** — par étape : pli, angle cible, V, plus deux drapeaux qui font tout le métier :

| Sigle | Nom | Ce que ça veut dire |
|---|---|---|
| **⇄** | `ButeeAval` | retournement **à plat**, bout pour bout. La face ne change pas, la butée lit le pan aval. |
| **⇅** | `Retournee` | retournement **dessus/dessous**. La face laquée change de côté, les plis déjà formés pointent à l'opposé. |

**Vue section** — matrice, poinçon (col de cygne compris), tôle à l'étape courante, cote butée,
hauteur libre. Bleu = tôle · vert = reprise · rouge = collision. Les sigles ⇄ / ⇅ s'affichent en
haut à droite, et une **main rouge** prévient dès qu'il reste moins de 50 mm à tenir côté opérateur.

**Vue développé** et **pupitre** (grille de séquence, style CN).

**Solveur d'ordre** (`Pliage/Solveur.cs`) — recherche exhaustive : toutes les combinaisons
ordre × sens d'engagement × retournements, chacune passée au vrai `Moteur` + `Detecteur`.
Garde ce qui ne tape rien, classe par : moins de retournements → **plus grande prise opérateur** →
moins de manip. Quelques dizaines de millisecondes sur un profil courant.

**Autotest** (`Pliage/Autotest.cs`) — les règles figées ne sont pas des commentaires, ce sont des
assertions qui tournent dans le build. Le chevêtre et le Z laqué sont la **référence** : si une
modif les fait tomber, c'est la modif qui est fausse.

---

## Les règles figées

Elles ne se rediscutent pas. Elles sont contrôlées par l'autotest à chaque étape des deux
pièces de référence.

1. **Sens** — le pan gauché **contre la butée** part **à DROITE**, quelle que soit sa taille.
   Le formage et l'opérateur sont à gauche. Sans exception, ⇄ ou ⇅ compris.
2. **Sommet** — le sommet du pli actif est à l'origine, sous la pointe du poinçon.
3. **Tous les plis vont vers le HAUT.** Le poinçon descend, la matrice tient, le volet monte.
   Les changements de direction viennent des **retournements**, jamais d'un pli vers le bas.
4. **Le contour du poinçon est figé** (relevé vectoriel). Seule `Hauteur` est réglable : le fût
   s'étire au-dessus de y = 60, le bec et le col de cygne restent intacts.
5. **Prise opérateur** — toujours **le plus grand côté vers l'opérateur** quand c'est possible.
   Tenir un bout de 20 mm, c'est les doigts au poinçon. Quand il n'y a pas le choix, la séquence
   reste faisable mais se classe en dernier, et la vue affiche la main rouge.

---

## Les deux pièces de référence

**Chevêtre** — 20 · 40 · 100 · 40 · 20, quatre plis à 90°, aucun retournement.
Ordre : pli 1 → pli 4 **⇄** → pli 2 → pli 3. Le ⇄ du pli 4 n'est pas une habitude : il envoie
le 20 à la butée et garde 200 mm en main.

**Z laqué** — 30 · 25 · 25 · 10. La face non laquée reste dessus tant qu'on peut, le laquage
est protégé dessous, puis on retourne pour le dernier pli.

| Étape | Pli | Angle | Sigle | Butée | Prise opérateur |
|---|---|---|---|---|---|
| 1 | le 10 | 45° | ⇄ | 10 | 80 mm |
| 2 | le 25 | 92° | ⇄ | 25 | 55 mm |
| 3 | le 30 | 90° | ⇅ | 30 | 60 mm |

Le 10 part à la butée pour garder les 80 en main. À l'étape 2, le retour du 10 déjà plié doit se
loger dans le **dégagement du col de cygne** (2,8 mm de demi-largeur à y = 17,5) — côté opérateur
le corps fait 8,7 mm et le retour taperait dedans.

---

## Architecture — trois couches

```
Materiel/   Plieuse.cs  Poincon.cs  Matrice.cs  Atelier.cs     le matériel physique
Pliage/     Piece.cs  Moteur.cs  Detecteur.cs  Solveur.cs      le calcul — zéro WinForms
            Autotest.cs  PieceIO.cs  Bibliotheque.cs
Vues/       Theme.cs  VueSection.cs  VueDeveloppe.cs           l'affichage seul
            VuePupitre.cs  FenetrePrincipale.cs
```

Le sens de dépendance est **Vues → Pliage → Materiel**, jamais l'inverse.

`Pliage/` et `Materiel/` n'ont **aucune dépendance UI** : ils compilent en console sur n'importe
quelle plateforme. C'est ce qui permet de faire tourner le moteur et l'autotest sans Windows —
et c'est aussi ce qui les rendra portables en JS pour le hub TOLTEM.

| Fichier | Rôle |
|---|---|
| `Materiel/Plieuse.cs` | cotes bâti, butée, hauteur libre. Presets Loire Safe 4 m, Amada 2 m. |
| `Materiel/Poincon.cs` | contour figé, hauteur réglable. |
| `Materiel/Matrice.cs` | profil ⊥ (tête + pied d'appui), vés, embases. |
| `Materiel/Atelier.cs` | la bibliothèque d'outillage, persistée en JSON. |
| `Pliage/Piece.cs` | pans, opérations, séquence, retournements. |
| `Pliage/Moteur.cs` | chaîne de la fibre neutre, ancrage sur le pli actif. |
| `Pliage/Detecteur.cs` | collisions contre l'outillage. |
| `Pliage/Solveur.cs` | recherche de l'ordre de pliage. |
| `Pliage/Autotest.cs` | contrôle des règles figées. |

---

## Outillage

**Poinçon — Rolleri P.150.35.R2**
Hauteur 150 (utile 120), bec 35° total (10° avant / 25° arrière), R2, col de cygne à dégagement
arrière. Contour figé au relevé vectoriel, pointe à (0,0).
Flanc droit (butée) **étroit** — c'est lui qui reçoit les retours déjà pliés.
Flanc gauche (opérateur) **large**, corps déporté.

**Matrices Euram** — profil en **T inversé (⊥)** : tête étroite où le vé est usiné, pied large
d'appui qui cale la matrice et l'allège.

| Preset | Tête | Pied | H | Vés |
|---|---|---|---|---|
| 2035 / 35° | 26 | 60 | 80 | V8, V12 |
| 2045 / 45° | 30 | 60 | 80 | V10, V12, V16, V20, V25 |
| Euram 2009 · 4 voies | 60×60 (bloc carré) | | 60 | V50 · 85°, V35 · 85°, V22 · 88°, V16 · 88° — R2 |

La 4 voies se tourne pour changer de vé : bloc carré, pas de ⊥.

**Plieuse — Loire Safe 4 m**

| Cote | Valeur |
|---|---|
| Butée arrière — profondeur | 10,2 → 695 mm |
| Butée arrière — course latérale | 100 → 2900 mm |
| Longueur de pli admissible | 100 → 4050 mm |
| Arcade (passage latéral) | 3000 mm |
| Hauteur libre ouverte | 120 mm |

La butée mini est un relevé au réglet, pas une loi : un pan de 10 se cale en vrai, même en 4 mm.
Le contrôle tolère 0,5 mm (`Detecteur.TolButee`).

Cotes encore à relever : voir **`COTES_A_MESURER.md`**.

---

## Build

Prérequis : **SDK .NET 8**.

Double-clic sur **`build.bat`** → exe autonome dans `exe_final\SimulateurPliage.exe`
(single-file, self-contained win-x64). Le `.bat` ferme l'appli si elle tourne, nettoie
`exe_final`, vérifie l'exe.

Ou :

```
dotnet publish SimulateurPliage.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o exe_final
```

**Le cœur se compile sans Windows.** `Pliage/` + `Materiel/` dans un projet console `net8.0`,
et `Autotest.Executer(...)` tourne sur Linux comme sur Mac. Pratique pour vérifier une modif de
géométrie avant de la pousser.

---

Suite : voir **`ROADMAP.md`**.

Made with ♥ by weapon666 pour TolTem.
