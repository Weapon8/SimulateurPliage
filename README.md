# Simulateur de pliage — collisions outillage

**WinForms .NET 8** (thème TolTem). Visualise une **séquence de pliage** en section 2D, étape par
étape, **détecte les collisions** pièce ↔ outillage — et **trouve l'ordre de pliage tout seul**.

Fait pour la plieuse **Loire Safe 4 m**, poinçon **Rolleri P.150.35.R2**, matrices Euram.
Toutes les cotes machine et outillage sont ajustables dans l'app.

> Le but n'est pas de remplacer le plieur. C'est de donner à quelqu'un qui débute l'ordre de
> pliage qu'un plieur expérimenté trouve d'instinct — et de le prévenir quand il va avoir les
> doigts près du bec, ou quand une aile va taper le tablier.

## But final du projet

Le cap, c'est la **saisie zéro** : **scanner l'image d'une pièce** (photo d'un profil, croquis
coté, plan) et en **déduire automatiquement** les plis, les angles et le **développé à plat** —
puis lancer le solveur dessus. Aujourd'hui l'opérateur saisit pans + angles + faces à la main ;
demain il prend une photo, l'outil lit la géométrie, et sort l'ordre de pliage. Tout ce qui est
construit (moteur, détecteur, solveur, convention faces/angles) est la brique de calcul sur
laquelle cette reconnaissance viendra se brancher.

---

## Convention d'angle (elle prime sur tout)

**180 = tôle à plat · 90 = équerre · 45 = pli aigu (fermé/serré).**
L'angle noté est **l'angle de PLI** (de combien on plie depuis le plat), pas l'angle intérieur
géométrique. Dans le code, la tôle tourne de `180 − AngleCible`.
⚠️ Le commentaire « angle intérieur » de `Piece.cs` est trompeur et sera corrigé.

---

## Ce que ça fait

**Saisie** — nombre de plis, épaisseur, longueur des pans, angles, faces.

**Séquence** — par étape : pli, angle cible, V, plus deux drapeaux qui font tout le métier :

| Sigle | Nom | Ce que ça veut dire |
|---|---|---|
| **⇄** | `ButeeAval` | retournement **à plat**, bout pour bout. La face ne change pas, la butée lit le pan aval. |
| **⇅** | `Retournee` | retournement **dessus/dessous**. La face laquée change de côté, les plis déjà formés pointent à l'opposé. |

**Vue section** — matrice, poinçon (col de cygne compris), tôle à l'étape courante, cote butée.
Bleu = tôle · vert = reprise · rouge = collision. Les sigles ⇄ / ⇅ s'affichent en haut à droite,
et une **main rouge** prévient dès qu'il reste moins de 50 mm à tenir côté opérateur.

**Vue développé** et **pupitre** (grille de séquence, style CN).

**Solveur d'ordre** (`Pliage/Solveur.cs`) — recherche exhaustive : toutes les combinaisons
ordre × sens d'engagement × retournements, chacune passée au vrai `Moteur` + `Detecteur`.
Garde ce qui ne tape rien, classe par : **pli fermé en premier** (contrainte dure) → moins de
retournements → **plus grande prise opérateur** → moins de manip. Quelques dizaines de ms sur un
profil courant.

**Autotest** (`Pliage/Autotest.cs`) — les règles figées ne sont pas des commentaires, ce sont des
assertions qui tournent dans le build. Le chevêtre, le Z laqué et la couvertine sont la
**référence** : si une modif les fait tomber, c'est la modif qui est fausse.

---

## Les règles figées

Elles ne se rediscutent pas. Elles sont contrôlées par l'autotest à chaque étape des pièces de
référence.

1. **Sens** — le pan gauché **contre la butée** part **à DROITE**, quelle que soit sa taille.
   Le formage et l'opérateur sont à gauche. Sans exception, ⇄ ou ⇅ compris.
2. **Sommet** — le sommet du pli actif est à l'origine, sous la pointe du poinçon.
3. **Tous les plis vont vers le HAUT.** Le poinçon descend, la matrice tient, le volet monte.
   Les changements de direction viennent des **retournements**, jamais d'un pli vers le bas.
4. **Le contour du poinçon est figé** (relevé vectoriel). Seule `Hauteur` est réglable : le fût
   s'étire au-dessus de y = 60, le bec et le col de cygne restent intacts.
5. **Prise opérateur** — toujours **le plus grand côté vers l'opérateur** quand c'est possible.
   Tenir un bout de 20 mm, c'est les doigts au poinçon. Sous 50 mm, la vue affiche la main rouge.
6. **Plancher d'angle** — on ne ferme pas plus que l'angle du vé. V16 de la 2045 à 45° → une casse
   à 44° est impossible en l'air, refusée à juste titre.
7. **Pli fermé en premier** *(nouveau — confirmé sur toute la prod TolTem)*. Le pli le plus aigu de
   la pièce est **toujours le premier geste** : il rigidifie la tôle, après on ne passe plus pour
   le former, ou le rebord déjà formé tape le tablier et on casse tôle + machine. Contrainte
   **dure** sur le 1er pli seulement — pas un ordre décroissant sur toute la séquence.

---

## Les pièces de référence

**Chevêtre** — 20 · 40 · 100 · 40 · 20, quatre plis à 90°, aucun retournement.

**Z laqué** — 30 · 25 · 25 · 10. La face non laquée reste dessus tant qu'on peut, le laquage est
protégé dessous, puis on retourne pour le dernier pli. Le 45° part en premier (règle 7).

**Couvertine** — 10 · 30 · 230 · 30 · 10, pince 45° · jambe 92° · fond · jambe 88° · goutte d'eau
163°, deux retournements. Le 45° (pince) part en premier.

---

## Vocabulaire machine (à ne pas confondre)

- **Tablier** = le **HAUT mobile** : coulisseau + « inter » (support poinçon) + poinçon.
- **Bâti + support matrice** = le **BAS fixe** = référence y = 0 sur la face matrice.
- **« Inter »** = support poinçon = `porte-poinçon` dans le code.
- **Raidisseur** = un pli à 45°. **Pli écrasé** (lèvre rabattue à fond) = formé autrement (machine
  à rives Jouanel), **hors périmètre plieuse**.

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
et c'est aussi ce qui les rendra portables en JS pour le hub TOLTEM, et branchables sous le futur
module de reconnaissance d'image.

---

## Outillage

**Poinçon — Rolleri P.150.35.R2**
Hauteur 150 (utile 120), bec 35° total (10° avant / 25° arrière), R2, col de cygne à dégagement
arrière. Contour figé au relevé vectoriel, pointe à (0,0).
Flanc droit (butée) **étroit** — c'est lui qui reçoit les retours déjà pliés.
Flanc gauche (opérateur) **large**, corps déporté.

**Matrices Euram** — profil en **T inversé (⊥)** : tête étroite où le vé est usiné, pied large
d'appui.

| Preset | Tête | Pied | H | Vés |
|---|---|---|---|---|
| 2035 / 35° | 26 | 60 | 80 | V8, V12 |
| 2045 / 45° | 30 | 60 | 80 | V10, V12, V16, V20, V25 |
| Euram 2009 · 4 voies | 60×60 (bloc carré) | | 60 | V50 · 85°, V35 · 85°, V22 · 88°, V16 · 88° — R2 |

**Plieuse — Loire Safe 4 m**

| Cote | Valeur | Utilisée par |
|---|---|---|
| Butée arrière — profondeur | 10,2 → 695 mm | butée mini (Detecteur) |
| Butée arrière — course latérale | 100 → 2900 mm | *(non lue)* |
| Longueur de pli admissible | 100 → 4050 mm | contrôle longueur (Detecteur) |
| Arcade (passage latéral) | 3000 mm | *(non lue)* |
| **Tablier — garde bec→tablier** | **280 mm** | **collision tablier (Detecteur)** |
| **Tablier — débord latéral** | **66 mm / côté** | collision tablier |

**Collision tablier** *(nouveau)* : une aile formée qui monte au-delà de 280 mm tape le haut du
tablier. La hauteur d'aile est la **vraie hauteur verticale** `L·sin(180−angle)`, pas la projection
du repère bissectrice. Une aile de 300 à 90° monte à 300 → tape ; à 45° elle monte à 212 → passe.
Marge de 5 mm pour la flexion des montants sous charge (1–2 mm mesurés).

**Aile mini** *(nouveau)* : plancher = `max(0,63 × V, butée mini)`, testé sur les **deux** pans
qui bordent le pli (formage ET butée).

La butée mini est un relevé au réglet, pas une loi : un pan de 10 se cale en vrai, même en 4 mm.
Le contrôle tolère 0,5 mm (`Detecteur.TolButee`) — c'est l'écart entre la cote programmée (ronde)
et la cote réelle.

Cotes encore à relever : voir **ROADMAP.md**.

---

## Build

Prérequis : **SDK .NET 8**.

Double-clic sur **`build.bat`** → exe autonome dans `exe_final\SimulateurPliage.exe`
(single-file, self-contained win-x64).

Ou :

```
dotnet publish SimulateurPliage.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o exe_final
```

**Le cœur se compile sans Windows.** `Pliage/` + `Materiel/` dans un projet console `net8.0`,
et `Autotest.Executer(...)` tourne sur Linux comme sur Mac. Indispensable pour vérifier une modif
de géométrie **au banc** avant de la pousser — c'est ce qui a évité de livrer des règles fausses.

---

Suite : voir **ROADMAP.md**.

Made with ♥ by weapon666 pour TolTem.
