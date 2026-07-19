# Simulateur de pliage — collisions outillage

**WinForms .NET 8** (thème TolTem). Visualise une **séquence de pliage** en section 2D et en 3D,
étape par étape, **détecte les collisions** pièce ↔ outillage — et **trouve l'ordre de pliage
tout seul**.

Fait pour la plieuse **Loire Safe 4 m**, poinçon **Rolleri P.150.35.R2**, matrices Euram.
Toutes les cotes machine et outillage sont ajustables dans l'app.

> Le but n'est pas de remplacer le plieur. C'est de donner à quelqu'un qui débute l'ordre de
> pliage qu'un plieur expérimenté trouve d'instinct — et de le prévenir quand il va avoir les
> doigts près du bec, ou quand une aile va taper le tablier.

## But final du projet

Le cap, c'est la **saisie zéro** : **scanner un croquis coté** (jamais une photo — un dessin coté
à la main ou un plan) et en **déduire automatiquement** les plis, les angles, les **faces** et le
**développé à plat** — puis lancer le solveur dessus. Aujourd'hui l'opérateur saisit pans + angles
+ faces à la main ; demain il scanne son croquis, l'outil lit la géométrie, et sort l'ordre de
pliage. Tout ce qui est construit (moteur, détecteur, solveur, convention faces/angles) est la
brique de calcul sur laquelle cette reconnaissance viendra se brancher.

---

## Convention d'angle (elle prime sur tout)

**180 = tôle à plat · 90 = équerre · 45 = pli aigu (fermé/serré).**
L'angle noté est **l'angle de PLI** (de combien on plie depuis le plat), pas l'angle intérieur
géométrique. Dans le code, la tôle tourne de `180 − AngleCible`.

---

## LA découverte de fond : la FACE commande tout

Après beaucoup de tâtonnements, une chose s'est imposée et structure désormais tout le moteur :

> **La FACE d'un pli (donnée de la pièce, lue sur le dessin, fixe) détermine à la fois la cote de
> butée, le sens du dessin et l'orientation de la tôle. Elle ne se confond PAS avec le
> retournement ⇄/⇅, qui est un geste de la séquence.**

- **FL** = côté brillant / laqué / galva traité = vers le visible = `true` = **violet**.
- **FNL** = l'autre côté (non laqué) = `false` = **bleu**.

Ce qui en découle, et qui est maintenant codé ainsi :

1. **Cote de butée par la face** — un pli **intérieur (FNL)** cale sur le pan **AVAL** ; un pli
   **extérieur (FL)** cale sur le pan **AMONT** ; le retournement à plat ⇄ inverse. C'est ce qui
   donne la vraie gamme du chéneau : **10 · 100 · 30 · 40 · 200**.
2. **Sens du dessin par la face** — un pli sur la même face que le pli actif est dessiné dans le
   même sens ; un pli sur la face opposée, en sens inverse. C'est ce qui donne la **forme**
   correcte (le U du chéneau) au lieu d'un zigzag. *(Avant, le sens suivait le drapeau `Retournee`
   — c'était la cause du chéneau plié en losange.)*
3. **Orientation par la face** — le pan **calé contre la butée** (le pan de cote) part à **DROITE**,
   le **grand corps de tôle** part à **GAUCHE** (opérateur). Comme le pan de cote dépend de la face
   ET du retournement, le grand pan va à gauche en pliage direct, et peut passer à droite quand la
   pièce est retournée — ce qui est le comportement réel en atelier.
4. **Couleur par les retournements cumulés** — la face **visible dessus** bascule à **chaque** ⇅
   depuis le début (parité). Deux ⇅ ramènent la face de départ. Le chéneau affiche donc
   bleu · bleu · bleu · violet · bleu.

---

## Ce que ça fait

**Saisie** — nombre de plis, épaisseur, longueur des pans, angles, faces.

**Table PLIS** — une ligne = **un pli** (dans l'ordre de la gamme), colonnes **Pli · Longueur ·
Angle · Face**. La longueur affichée est le **pan qu'on replie** (= la cote de butée du pli) : le
raidisseur 45° apparaît bien sur le pli du 10, plus sur le mauvais pan. Un clic sur la colonne Face
bascule FNL ↔ FL.

**Séquence** — par étape : pli, angle cible, V, plus deux drapeaux — mais qui ne décident **plus**
de la face (c'est la propriété `Faces` de la pièce qui fait foi) :

| Sigle | Nom | Ce que ça veut dire |
|---|---|---|
| **⇄** | `ButeeAval` | retournement **à plat**, bout pour bout. |
| **⇅** | `Retournee` | retournement **dessus/dessous**. Fait basculer la face visible (couleur). |

**Vue section** et **vue 3D** — matrice, poinçon (col de cygne compris), tôle à l'étape courante,
cote butée. **Bleu = FNL dessus · violet = FL dessus · vert = reprise · rouge = collision.** Les
sigles ⇄ / ⇅ s'affichent en haut à droite, et une **main rouge** prévient dès qu'il reste peu à
tenir côté opérateur (uniquement quand un pli franchement aigu est concerné).

**Vue développé** et **pupitre** (grille de séquence, style CN).

**Solveur d'ordre** (`Pliage/Solveur.cs`) — **branché dans l'UI** (bouton « Ordre auto »).
Recherche exhaustive : toutes les combinaisons ordre × sens × retournements, chacune passée au vrai
`Moteur` + `Detecteur`. Garde ce qui ne tape rien, classe par : **pli fermé en premier**
(contrainte dure) → moins de retournements → **plus grande prise opérateur** → moins de manip.

**Autotest** (`Pliage/Autotest.cs`) — les règles figées ne sont pas des commentaires, ce sont des
assertions qui tournent dans le build. Le chevêtre, le Z laqué et la couvertine sont la
**référence** : si une modif les fait tomber, c'est la modif qui est fausse. **22 contrôles.**

---

## Les pièces de référence — figées en JSON

Les pièces validées à l'atelier sont **sorties du code en dur** : leur géométrie, leurs faces et
leurs gammes vivent dans **`Pliage/ProduitReference.cs`** (une constante JSON). La bibliothèque les
lit au démarrage et réinjecte celles qui manquent. **On ne retouche plus le moteur pour une pièce
de référence** ; si l'une doit changer, on régénère ce JSON à part.

**Chevêtre** — 20 · 40 · 100 · 40 · 20, quatre plis à 90°, aucun retournement, toutes faces FNL.
Cotes de butée `20 · 20 · 40 · 100`.

**Z laqué** — 30 · 25 · 25 · 10. La face non laquée reste dessus tant qu'on peut, puis on retourne
pour le dernier pli. Cotes `10 · 25 · 30`.

**Couvertine** — 10 · 30 · 230 · 30 · 10, deux retournements. Cotes `10 · 30 · 10 · 30`.

**Chéneau** — 30 · 40 · 150 · 200 · 100 · 10, faces `FNL · FL · FNL · FNL · FNL`, raidisseur 45° en
premier. Gamme **10 · 100 · 30 · 40 · 200**. À l'étape 5, le pli du 200 vient **buter sur le pli
du 100** déjà formé (2ᵉ butée). C'est la pièce qui a servi à démêler face / butée / sens /
orientation.

---

## Les règles figées

Elles ne se rediscutent pas. Elles sont contrôlées par l'autotest à chaque étape des pièces de
référence.

1. **Orientation** — le pan **calé contre la butée** (pan de cote) part à **DROITE** ; le **grand
   corps** part à **GAUCHE** (opérateur). En pliage direct le grand pan est à gauche ; avec
   retournement/appui il peut passer à droite — c'est correct. *(L'autotest vérifie « grand côté à
   gauche » aux étapes directes.)*
2. **Sommet** — le sommet du pli actif est à l'origine, sous la pointe du poinçon.
3. **Tous les plis vont vers le HAUT.** Le poinçon descend, la matrice tient, le volet monte.
   Les changements de direction viennent des **faces** et des retournements, jamais d'un pli vers
   le bas.
4. **Le contour du poinçon est figé** (relevé vectoriel). Seule `Hauteur` est réglable.
5. **Prise opérateur** — toujours **le plus grand côté vers l'opérateur** quand c'est possible.
6. **Plancher d'angle** — on ne ferme pas plus que l'angle du vé.
7. **Pli fermé en premier.** Le pli le plus aigu de la pièce est **toujours le premier geste** :
   il rigidifie la tôle. Contrainte **dure** sur le 1er pli seulement.

---

## Vocabulaire machine (à ne pas confondre)

- **Tablier** = le **HAUT mobile** : coulisseau + « inter » (support poinçon) + poinçon.
- **Bâti + support matrice** = le **BAS fixe** = référence y = 0 sur la face matrice.
- **« Inter »** = support poinçon = `porte-poinçon` dans le code.
- **Raidisseur** = un pli à 45°. **Pli écrasé** = formé autrement (Jouanel), **hors périmètre
  plieuse**.

---

## Architecture — trois couches

```
Materiel/   Plieuse.cs  Poincon.cs  Matrice.cs  Atelier.cs     le matériel physique
Pliage/     Piece.cs  Moteur.cs  Detecteur.cs  Solveur.cs      le calcul — zéro WinForms
            Autotest.cs  PieceIO.cs  Bibliotheque.cs
            ProduitReference.cs   (les pièces de référence figées en JSON)
Vues/       Theme.cs  VueSection.cs  VueVolume.cs               l'affichage seul
            VueDeveloppe.cs  VuePupitre.cs  FenetrePrincipale.cs
```

Le sens de dépendance est **Vues → Pliage → Materiel**, jamais l'inverse.

`Pliage/` et `Materiel/` n'ont **aucune dépendance UI** : ils compilent en console sur n'importe
quelle plateforme. C'est ce qui permet de faire tourner le moteur et l'autotest sans Windows —
et c'est aussi ce qui les rendra portables en JS pour le hub TOLTEM, et branchables sous le futur
module de scan de croquis.

---

## Outillage

**Poinçon — Rolleri P.150.35.R2**
Hauteur 150 (utile 120), bec 35° total (10° avant / 25° arrière), R2, col de cygne à dégagement
arrière. Contour figé au relevé vectoriel, pointe à (0,0).
Flanc droit (butée) **étroit** — il reçoit les retours déjà pliés.
Flanc gauche (opérateur) **large**, corps déporté.

**Matrices Euram** — profil en **T inversé (⊥)** : tête étroite où le vé est usiné, pied large.

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

**Collision tablier** : une aile formée qui monte au-delà de 280 mm tape le haut du tablier. La
hauteur d'aile est la **vraie hauteur verticale** `L·sin(180−angle)`, pas la projection du repère
bissectrice. Marge de 5 mm pour la flexion des montants.

**Aile mini** : plancher = `max(0,63 × V, butée mini)`, testé sur les **deux** pans qui bordent le
pli. La butée mini tolère 0,5 mm (`Detecteur.TolButee`).

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
