# Roadmap — Simulateur de pliage

État au 15/07/2026. Ce qui est **fait** est vérifié par `Pliage/Autotest.cs` sur les deux pièces
de référence (chevêtre + Z laqué) — pas « ça a l'air bon », contrôlé.

---

## Fait

**Socle**
- [x] Refactor trois couches `Materiel/` · `Pliage/` · `Vues/`, dépendances à sens unique.
- [x] `Pliage/` et `Materiel/` sans aucune dépendance WinForms → compilables en console,
      testables hors Windows, portables en JS plus tard.
- [x] Bibliothèque d'outillage persistée (`atelier.json`), presets plieuse / poinçon / matrice.

**Géométrie**
- [x] Ancrage bissectrice, sommet du pli actif à l'origine.
- [x] Règle de sens figée : le pan contre la butée toujours à droite.
- [x] Retournement ⇅ : seuls les plis **déjà formés** s'inversent. Le pli actif va toujours vers
      le haut — le poinçon descend, point. *(Le bug qui repliait le volet actif dans le même sens
      que le déjà-plié et collait une spirale sur le bec.)*
- [x] Retournement à plat ⇄ : la butée lit le pan aval.
- [x] Contour poinçon figé, seule la hauteur étire le fût.
- [x] Matrices en profil ⊥ (tête + pied), 4 voies pour la 2009.

**Détection**
- [x] Collisions poinçon / matrice / porte-poinçon / semelle, repli sur repli, limites de butée.
- [x] Seuls les **retours déjà pliés** sont testés : au premier pli il n'existe aucun coude
      antérieur, donc rien ne peut taper le bec. *(Fin des faux positifs sur un 45° avec un
      bec à 35°.)*
- [x] Epsilon sur l'intersection : deux segments colinéaires ne sont pas un croisement.
      *(Fin du faux « repli sur repli ».)*
- [x] Tolérance sur la butée mini (`Detecteur.TolButee`) : un pan de 10 se cale en vrai.

**Solveur**
- [x] Recherche exhaustive ordre × sens d'engagement × retournements, chaque candidat passé au
      vrai `Moteur` + `Detecteur` — pas de géométrie recalculée à côté, donc zéro divergence.
- [x] Parité de face : les retournements ne sont pas un choix libre, ils sont imposés par la
      forme finale. Le solveur les déduit.
- [x] Règles de calage métier : flan mini, pan porteur d'un retour déjà plié, butée mini.
- [x] **Classement sécurité** : le maillon faible décide. Une séquence qui laisse 20 mm en main
      passe derrière une qui en laisse 200, même si elle a moins de manip.
- [x] Retrouve les deux séquences de référence sans qu'on lui souffle rien.

**Sécurité opérateur**
- [x] `Solveur.PriseOperateur()` : ce que l'opérateur tient devant lui.
- [x] Main rouge dans la vue section sous 50 mm (`VueSection.PriseAlerte`).

**Contrôle**
- [x] `Autotest.cs` — les règles figées deviennent des assertions. 14 contrôles, un clic.

---

## En cours

- [ ] **Brancher le solveur dans l'UI.** `Solveur.Resoudre(...)` tourne, il lui manque un bouton
      et un panneau qui affiche le top 3 avec, pour chaque séquence : retournements, prise mini,
      et un bouton « appliquer ».
- [ ] **Colonne Face dans la grille PANS.** Le solveur a besoin de la face de chaque pli
      (NL / FL, ou ↑ / ↓) pour déduire les retournements sur un profil quelconque. Aujourd'hui
      elle est lue depuis le drapeau `Retournee` de la séquence — ça marche pour les démos,
      pas pour une pièce saisie de zéro.
- [ ] **Bouton Autotest** dans le panneau FICHIER.

---

## À faire

**Métier**
- [ ] **Appui sur pli déjà formé** comme vrai mode de butée. Le retour du 10 qui vient contre le
      doigt, ce n'est pas la même chose qu'un pan à plat. Aujourd'hui la cote R affichée reste le
      modèle **1 pan** : elle ne compte que le segment collé au pli, alors qu'un enchaînement de
      pans à plat forme une seule course. À corriger en même temps.
- [ ] **Trancher le pli 3 du chevêtre.** En direct il laisse 60 mm en main, le solveur le passe
      en ⇄ et en laisse 160. Les deux sont propres côté collision. Question de plieur, pas de code.
- [ ] **Allongement / fibre neutre** — cotes précises de débit.
- [ ] **Tonnage** : refuser une séquence qui dépasse le tonnage machine ou le t/m du poinçon.

**Outillage**
- [ ] `Plieuse.TonnageMax` et `Plieuse.DoigtHauteur` sont à 0 — à relever.
- [ ] Cotes de l'**Amada 2 m** : toutes estimées.
- [ ] Cotes matrice Euram : tête / pied / hauteur viennent d'un relevé sur photo. Un coup de
      réglet et on les fige.

**Plus loin**
- [ ] **Import** depuis DeveloppeProfil / DeveloppeCheneau (segments + plis).
- [ ] Marquage poinçon.
- [ ] Vue 3D décomposée.
- [ ] Version HTML autonome pour le hub TOLTEM (le cœur est déjà sans UI, c'est faisable).

---

## Ce qu'on ne refait pas

Les règles figées du README. Elles ont coûté cher à trouver, elles sont contrôlées par
l'autotest, et un commentaire qui dit « RÈGLE FIGÉE » n'arrête personne — seule une assertion
qui tourne le fait.

Et une leçon qui vaut pour la suite : **les séquences approuvées sont la référence, pas une
question ouverte.** Si le solveur ne remet pas le chevêtre et le Z en tête, c'est le solveur
qui est faux.

Made with ♥ by weapon666 pour TolTem.
