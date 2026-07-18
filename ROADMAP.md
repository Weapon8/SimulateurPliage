# Roadmap — Simulateur de pliage

État au 18/07/2026. Ce qui est **fait** est vérifié par `Pliage/Autotest.cs` sur les pièces de
référence (chevêtre + Z laqué + couvertine) — pas « ça a l'air bon », contrôlé au banc.

---

## 🎯 Cap final : la saisie zéro (scan image → développé)

Le but du projet : **photographier une pièce ou un croquis coté et en déduire automatiquement
les plis, les angles et le développé à plat**, puis lancer le solveur dessus. L'opérateur ne
saisit plus rien à la main — il prend une photo.

Décomposition prévue (rien de commencé, c'est le cap) :
- [ ] **Lecture d'image** — détecter le profil (trait de la tôle) sur une photo / un scan de croquis.
- [ ] **Extraction géométrie** — repérer les segments (pans) et les sommets (plis).
- [ ] **Mesure des angles** — angle de pli à chaque sommet (convention 180 = plat).
- [ ] **Lecture des cotes** — longueurs de pans (OCR des cotes chiffrées, ou échelle du quadrillage).
- [ ] **Déduction des faces** — le sens de chaque coin donne la face (voir MÉTHODE : la face
      découle de la géométrie, un Z alterne, un profil qui serpente bascule).
- [ ] **Développé à plat** — reconstruire le flan déplié à partir des pans + rayons.
- [ ] **Branchement solveur** — passer le tout à `Solveur.Resoudre(...)`, déjà prêt.

Tout le socle actuel (moteur, détecteur, solveur, convention faces/angles, cotes machine) est la
brique de calcul sur laquelle cette reconnaissance se branchera. C'est pour ça qu'il est sans UI
et testé au banc : réutilisable tel quel.

---

## Fait

**Socle**
- [x] Refactor trois couches `Materiel/` · `Pliage/` · `Vues/`, dépendances à sens unique.
- [x] `Pliage/` et `Materiel/` sans dépendance WinForms → compilables en console, testables hors
      Windows, portables en JS, branchables sous le futur module image.
- [x] Bibliothèque d'outillage persistée (`atelier.json`), presets plieuse / poinçon / matrice.

**Géométrie**
- [x] Ancrage bissectrice, sommet du pli actif à l'origine.
- [x] Règle de sens figée : le pan contre la butée toujours à droite.
- [x] Retournement ⇅ : seuls les plis **déjà formés** s'inversent. Le pli actif va toujours vers
      le haut.
- [x] Retournement à plat ⇄ : la butée lit le pan aval.
- [x] Contour poinçon figé, seule la hauteur étire le fût.
- [x] Matrices en profil ⊥ (tête + pied), 4 voies pour la 2009.
- [x] **Convention d'angle clarifiée** : 180 = plat, 90 = équerre, 45 = aigu. AngleCible = angle
      de PLI, tôle tourne de 180 − AngleCible.

**Détection**
- [x] Collisions poinçon / matrice / porte-poinçon / semelle, repli sur repli, limites de butée.
- [x] Seuls les **retours déjà pliés** sont testés (pas de faux positif au 1er pli).
- [x] Epsilon sur l'intersection (fin du faux « repli sur repli »).
- [x] Tolérance sur la butée mini (`Detecteur.TolButee`) : un pan de 10 se cale en vrai.
- [x] **Aile mini** = `max(0,63 × V, butée mini)`, testée sur les deux pans (formage + butée).
- [x] **Longueur de pli** : refus hors 100–4050 mm (cotes machine enfin relues).
- [x] **Collision tablier** : aile qui dépasse 280 mm (garde bec→tablier) tape le haut. Hauteur
      d'aile VRAIE `L·sin(180−angle)`, pas la projection bissectrice. Marge flexion 5 mm.
      Débord latéral 66 mm/côté. Testé : aile 300 @ 90° tape, @ 45° passe.

**Solveur**
- [x] Recherche exhaustive ordre × sens × retournements, chaque candidat au vrai Moteur +
      Detecteur — zéro divergence avec l'écran.
- [x] Parité de face : les retournements sont imposés par la forme, le solveur les déduit.
- [x] Règles de calage métier : flan mini, pan porteur, butée mini.
- [x] **Classement sécurité** : le maillon faible décide (prise mini la plus grande d'abord).
- [x] **Contrainte dure « pli fermé en premier »** : le pli le plus aigu est le 1er geste, sur le
      1er pli seulement (pas d'ordre décroissant forcé — casserait la couvertine).
- [x] Retrouve les séquences de référence, toutes « fermé d'abord ».
- [x] Validé au banc sur pièces réelles : chéneau (5 plis + raidisseur), coiffe.

**Sécurité opérateur**
- [x] `Solveur.PriseOperateur()` : ce que l'opérateur tient devant lui.
- [x] Main rouge dans la vue section sous 50 mm (`VueSection.PriseAlerte`).

**Contrôle**
- [x] `Autotest.cs` — règles figées → assertions. 22 contrôles, un clic.

---

## En cours

- [ ] **Brancher le solveur dans l'UI** *(point clé)*. `Solveur.Resoudre(...)` tourne, testé,
      mais aucun bouton ne le déclenche. Il faut un panneau qui affiche le top 3 avec, par
      séquence : retournements, prise mini, et un bouton « appliquer ».
      ⚠️ Point critique : les **faces** passées au solveur doivent venir du profil réel, jamais
      d'une case cochée au hasard — sinon il résout des pièces impossibles (cf. Z spirale).
- [ ] **Alerte pupitre « pli n°1 »** : si l'opérateur change le premier pli (qui doit être le plus
      fermé), afficher une alerte dure. La règle 7 doit être visible, pas seulement dans le solveur.
- [ ] **Colonne Face dans la grille PANS** : le solveur a besoin de la face de chaque pli. Lue
      aujourd'hui depuis le drapeau `Retournee` de la séquence (ok pour les démos, pas pour une
      pièce saisie de zéro). Étape obligée avant le module image.
- [ ] **Bouton Autotest** dans le panneau FICHIER.

---

## À faire

**Métier**
- [ ] **Appui sur pli déjà formé** comme vrai mode de butée (le retour qui vient contre le doigt
      ≠ pan à plat). La cote R reste le modèle 1 pan.
- [ ] **Allongement / fibre neutre** — cotes précises de débit (indispensable pour le développé).
- [ ] **Tonnage** : refuser une séquence qui dépasse le tonnage machine ou le t/m du poinçon.

**Corrections connues**
- [ ] **Commentaire trompeur `Piece.cs`** : « angle intérieur visé » → c'est l'angle de PLI.
- [ ] **Preset Amada** : `TonnageMax` non défini → hérite 180 t (Loire Safe) au lieu de 50 t
      mesurés. Gelé pour l'instant (on reste sur la Loire Safe), à corriger avec bump
      `CURRENT_VERSION`.

**Outillage — cotes à relever sur la Loire Safe**
- [ ] `Plieuse.TonnageMax` (180 t supposé, à lire sur la plaque).
- [ ] `Poincon.TonnageParMetre` (70 t/m supposé — gravé sur le corps).
- [ ] `Plieuse.DoigtHauteur` (hauteur des doigts de butée au-dessus de la face matrice).
- [ ] Cotes **Amada 2 m** : toutes estimées.
- [ ] Cotes matrice Euram (tête/pied/hauteur, relevé photo).

**Plus loin**
- [ ] **Module de reconnaissance d'image** (voir cap final ci-dessus) — le gros morceau.
- [ ] Import depuis DeveloppeProfil / DeveloppeCheneau (segments + plis).
- [ ] Marquage poinçon.
- [ ] Vue 3D décomposée.
- [ ] Version HTML autonome pour le hub TOLTEM.

---

## Ce qu'on ne refait pas

Les règles figées du README. Elles ont coûté cher, sont contrôlées par l'autotest, et un
commentaire « RÈGLE FIGÉE » n'arrête personne — seule une assertion qui tourne le fait.

Deux leçons de la session chéneau/coiffe (18/07), à ne pas réapprendre :

1. **La face découle de la géométrie, elle ne se choisit pas.** Deviner les faces au lieu de les
   lire sur le dessin = la faute qui a fait tourner en rond (Z spirale, chéneau à « 0
   retournement »). Toujours : faces d'abord (lues), puis cotes, puis angles.

2. **Quand le code et l'atelier divergent, c'est le code qui a tort.** Weapon dit vrai. Une aile
   de 300 tape le tablier même si la trigo du repère bissectrice dit qu'elle monte à 212 — c'était
   le repère qui était faux, pas la tôle. Vérifier au banc, ne pas redemander vingt fois.

Made with ♥ by weapon666 pour TolTem.
