# Roadmap — Simulateur de pliage

État au 19/07/2026. Ce qui est **fait** est vérifié par `Pliage/Autotest.cs` sur les pièces de
référence (chevêtre + Z laqué + couvertine) — pas « ça a l'air bon », contrôlé au banc.

---

## 🎯 Cap final : la saisie zéro (scan croquis coté → développé)

Le but du projet : **scanner un croquis coté** (dessin à la main coté, plan — **jamais une photo**)
et en déduire automatiquement **les plis, les angles, les faces et le développé à plat**, puis
lancer le solveur dessus. L'opérateur ne saisit plus rien à la main.

Décomposition prévue (rien de commencé, c'est le cap) :
- [ ] **Lecture du croquis** — détecter le profil (trait de la tôle) sur le scan, sur fond quadrillé.
- [ ] **Extraction géométrie** — repérer les segments (pans) et les sommets (plis).
- [ ] **Mesure des angles** — angle de pli à chaque sommet (convention 180 = plat).
- [ ] **Lecture des cotes** — longueurs de pans (OCR des cotes chiffrées, ou échelle du quadrillage).
- [ ] **Déduction AUTO des faces** — c'est le point qui change tout : la face de chaque pli doit se
      **déduire de la géométrie du tracé** (le sens de chaque coin), plus jamais être devinée ni
      cochée à la main. Un profil qui serpente bascule de face ; un U garde la même. Fin des
      devinettes.
- [ ] **Développé à plat** — reconstruire le flan déplié à partir des pans + rayons.
- [ ] **Branchement solveur** — passer le tout à `Solveur.Resoudre(...)`, déjà prêt et déjà branché
      dans l'UI.

Tout le socle actuel (moteur, détecteur, solveur, convention faces/angles, cotes machine) est la
brique de calcul sur laquelle cette reconnaissance se branchera. C'est pour ça qu'il est sans UI
et testé au banc : réutilisable tel quel.

---

## Fait

**Socle**
- [x] Refactor trois couches `Materiel/` · `Pliage/` · `Vues/`, dépendances à sens unique.
- [x] `Pliage/` et `Materiel/` sans dépendance WinForms → compilables en console, testables hors
      Windows, portables en JS, branchables sous le futur module croquis.
- [x] Bibliothèque d'outillage persistée (`atelier.json`), presets plieuse / poinçon / matrice.
- [x] **Pièces de référence figées en JSON** (`Pliage/ProduitReference.cs`) — sorties du code en
      dur. La bibliothèque les lit au démarrage. On ne retouche plus le moteur pour une référence ;
      si l'une doit changer, on régénère le JSON. Chevêtre, Z laqué, couvertine, chéneau.

**La face commande tout** *(la grande avancée de la session 18–19/07)*
- [x] **Cote de butée par la face** : pli intérieur (FNL) → pan aval ; pli extérieur (FL) → pan
      amont ; ⇄ inverse. Donne la vraie gamme du chéneau `10 · 100 · 30 · 40 · 200`.
- [x] **Sens du dessin par la face** : un pli suit le sens de sa face (même face que l'actif → même
      sens, face opposée → inverse). Corrige la **forme** (le chéneau faisait un losange quand le
      sens suivait le drapeau `Retournee`). Version validée à l'atelier : à l'étape 5, le 200 vient
      buter sur le 100.
- [x] **Orientation par la face** : le pan de cote (calé contre la butée) part à droite, le grand
      corps à gauche (opérateur). Grand pan à gauche en direct, à droite avec retournement/appui.
- [x] **Couleur par retournements cumulés** : la face visible bascule à chaque ⇅ (parité). Chéneau
      = bleu · bleu · bleu · violet · bleu. Champ `EtatEtape.Piece` + `Piece.FaceDessusFNL(etape)`.

**Géométrie**
- [x] Ancrage bissectrice, sommet du pli actif à l'origine.
- [x] Retournement ⇅ : seuls les plis **déjà formés** s'inversent. Le pli actif va toujours vers
      le haut.
- [x] Retournement à plat ⇄ : la butée lit le pan aval.
- [x] Contour poinçon figé, seule la hauteur étire le fût.
- [x] Matrices en profil ⊥ (tête + pied), 4 voies pour la 2009.
- [x] **Convention d'angle clarifiée** : 180 = plat, 90 = équerre, 45 = aigu. AngleCible = angle
      de PLI, tôle tourne de 180 − AngleCible. Commentaire `Piece.cs` corrigé.

**Détection**
- [x] Collisions poinçon / matrice / porte-poinçon / semelle, repli sur repli, limites de butée.
- [x] Seuls les **retours déjà pliés** sont testés (pas de faux positif au 1er pli).
- [x] Epsilon sur l'intersection (fin du faux « repli sur repli »).
- [x] Tolérance sur la butée mini (`Detecteur.TolButee`) : un pan de 10 se cale en vrai.
- [x] **Aile mini** = `max(0,63 × V, butée mini)`, testée sur les deux pans (formage + butée).
- [x] **Longueur de pli** : refus hors 100–4050 mm.
- [x] **Collision tablier** : aile qui dépasse 280 mm tape le haut. Hauteur d'aile VRAIE
      `L·sin(180−angle)`. Marge flexion 5 mm. Débord latéral 66 mm/côté.

**Solveur**
- [x] Recherche exhaustive ordre × sens × retournements, chaque candidat au vrai Moteur +
      Detecteur — zéro divergence avec l'écran.
- [x] Parité de face : les retournements sont imposés par la forme, le solveur les déduit.
- [x] Règles de calage métier : flan mini, pan porteur, butée mini.
- [x] **Classement sécurité** : le maillon faible décide (prise mini la plus grande d'abord).
- [x] **Contrainte dure « pli fermé en premier »** sur le 1er pli seulement.
- [x] **BRANCHÉ DANS L'UI** : bouton « Ordre auto » → `Solveur.Resoudre(...)`. Le doublon
      (mini-solveur maison qui court-circuitait le vrai solveur) a été supprimé.
- [x] Retrouve les séquences de référence, toutes « fermé d'abord ».

**Interface**
- [x] **Table PLIS** (ex-« PANS ») : une ligne = un pli, colonnes Pli · Longueur · Angle · Face.
      Longueur = le pan qu'on replie (= cote de butée). Le raidisseur 45° tombe sur le bon pli.
- [x] **Colonne Face éditable** : clic bascule FNL ↔ FL, drapeau `FacesManuelles` (la saisie fait
      foi et n'est plus réécrite depuis `Retournee`).

**Sécurité opérateur**
- [x] `Solveur.PriseOperateur()` : ce que l'opérateur tient devant lui.
- [x] Main rouge dans la vue section, **seulement si un pli franchement aigu (≤45°) existe et n'est
      pas en tête** (fini les fausses alertes sur un profil tout à 90°).

**Contrôle**
- [x] `Autotest.cs` — règles figées → assertions. 22 contrôles, un clic (banc).

---

## En cours / à faire côté UI

- [ ] **Bouton Autotest** dans le panneau FICHIER (aujourd'hui l'autotest ne tourne qu'au banc).
- [ ] **Alerte pupitre « pli n°1 »** : si l'opérateur change le premier pli (qui doit être le plus
      fermé), afficher une alerte dure. La règle 7 doit être visible dans le pupitre, pas seulement
      dans le solveur.

---

## À faire

**Métier**
- [ ] **Appui sur pli déjà formé** comme vrai mode de butée (le retour qui vient contre le doigt
      ≠ pan à plat). Amorcé sur le chéneau (`PliAppui` tient compte du retournement) ; à
      généraliser proprement. La cote R reste le modèle 1 pan.
- [ ] **Allongement / fibre neutre** — cotes précises de débit (indispensable pour le développé).
- [ ] **Tonnage** : refuser une séquence qui dépasse le tonnage machine ou le t/m du poinçon.

**Corrections connues**
- [ ] **Preset Amada** : `TonnageMax` non défini → hérite 180 t (Loire Safe) au lieu de 50 t
      mesurés. Gelé (on reste sur la Loire Safe), à corriger avec bump `CURRENT_VERSION`.

**Outillage — cotes à relever sur la Loire Safe**
- [ ] `Plieuse.TonnageMax` (180 t supposé, à lire sur la plaque).
- [ ] `Poincon.TonnageParMetre` (70 t/m supposé — gravé sur le corps).
- [ ] `Plieuse.DoigtHauteur` (hauteur des doigts de butée au-dessus de la face matrice).
- [ ] Cotes **Amada 2 m** : toutes estimées.
- [ ] Cotes matrice Euram (tête/pied/hauteur, relevé photo).

**Plus loin**
- [ ] **Module de scan de croquis coté** (voir cap final ci-dessus) — le gros morceau. Les faces
      s'y déduisent AUTO de la géométrie.
- [ ] Import depuis DeveloppeProfil / DeveloppeCheneau (segments + plis).
- [ ] Marquage poinçon.
- [ ] Version HTML autonome pour le hub TOLTEM.

---

## Ce qu'on ne refait pas

Les règles figées du README. Elles ont coûté cher, sont contrôlées par l'autotest, et un
commentaire « RÈGLE FIGÉE » n'arrête personne — seule une assertion qui tourne le fait.

Les leçons de la session chéneau (18–19/07), à ne pas réapprendre :

1. **La face découle de la géométrie, elle ne se choisit pas.** Deviner les faces au lieu de les
   lire sur le dessin = la faute qui a fait tourner en rond pendant des jours. Toujours : faces
   d'abord (lues), puis cotes, puis angles.

2. **La face n'est PAS le retournement.** La face est une propriété fixe de la pièce (le côté
   laqué). Le retournement ⇄/⇅ est un geste de la séquence. Le vieux code les mélangeait partout
   (butée, sens du dessin, couleur, orientation) — c'était la source de presque tous les bugs. La
   cote, le sens, l'orientation se calculent depuis la **face** ; seule la couleur suit la parité
   des retournements.

3. **Quand le code et l'atelier divergent, c'est le code qui a tort.** Weapon dit vrai. Vérifier au
   banc, ne pas redemander vingt fois une réponse déjà donnée.

4. **Juger une forme sur des coordonnées de repère incliné est impossible.** Un U et un losange ont
   des coordonnées voisines dans le repère bissectrice. Ne pas deviner : faire un rendu et
   demander.

Made with ♥ by weapon666 pour TolTem.
