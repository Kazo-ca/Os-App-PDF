# Kazo Pdf

Un lecteur et éditeur de PDF pour Kazo OS. Kazo Pdf est basé sur
[PdfCraft](https://github.com/storytold/pdfcraft) de l'équipe ArtCraft, un éditeur PDF libre écrit
en Rust, qui tourne ici dans une fenêtre de Kazo OS.

## Lire et modifier

- Lire, chercher, zoomer, parcourir les pages, les signets et les vignettes.
- Annoter : surligner, commenter, dessiner, tampons, mesures.
- Modifier le texte et les images, réorganiser, faire pivoter, insérer, extraire et supprimer des
  pages, combiner plusieurs fichiers.
- Remplir et créer des formulaires, signer, protéger par mot de passe, caviarder.
- Reconnaître le texte d'un document numérisé (OCR) : les modèles sont livrés avec l'app, rien ne
  part sur Internet.
- Plusieurs fenêtres à la fois, chacune avec ses documents.

## Vos fichiers Kazo OS

- Double-cliquez sur un fichier `.pdf` dans l'Explorateur de fichiers : il s'ouvre dans Kazo Pdf.
- Enregistrer réécrit le PDF ouvert. Enregistrer sous (Ctrl+Maj+S) et le premier
  enregistrement d'un nouveau document passent par le dialogue d'enregistrement de Kazo OS.
- Les exports (Word, images, texte, Imprimer en PDF, Réduire la taille, copie signée, données de
  formulaire, résultat de l'OCR…) passent par le dialogue « Enregistrer une copie » de Kazo OS :
  le document reste lié à son fichier.
- Fermer la fenêtre demande avant de perdre des modifications, pour chaque document ouvert.
- Fichier › Ouvrir, les images, les polices et les identités numériques à insérer viennent des
  fichiers Kazo OS, par le dialogue de Kazo OS.
- Kazo Pdf suit le thème (clair ou sombre) et la langue de l'OS ; ses réglages sont gardés par
  utilisateur Kazo OS dans `~/.config/ca.kazo.pdf/`.

## Limites connues

- **PdfCraft est en alpha.** La plupart des documents s'ouvrent et s'enregistrent bien, mais gardez
  une copie des plus importants.
- **Écritures asiatiques** : la version web de PdfCraft n'a pas de police chinoise, japonaise ou
  coréenne ; ce texte peut s'afficher en carrés dans l'interface, et y écrire du japonais dans un
  PDF renvoie une erreur.
- PdfCraft dessine avec **WebGL 2** : un navigateur ou un ordinateur sans WebGL 2 montre un écran
  d'erreur au lieu du document.
- Le téléchargement fait environ 34 Mo (77 Mo une fois installé), et chaque fenêtre charge sa
  propre copie de PdfCraft. La reconnaissance de texte demande beaucoup de mémoire (environ 600 Mo
  par tâche).
- La fenêtre reste sur l'écran où elle s'est ouverte : la passer sur un autre écran rechargerait
  PdfCraft et perdrait les documents ouverts.
- Demande Kazo OS 3.3.4 ou plus récent.

## Licence

Kazo Pdf est un logiciel libre sous licence MIT. PdfCraft est sous licence MIT ou Apache-2.0 ; ses
avis, les licences des polices qu'il contient et celle des modèles de reconnaissance de texte
(CC-BY-SA-4.0, Robert Knight) sont livrés avec l'app (voir `THIRD-PARTY-NOTICES-fr.md` dans le
dépôt). Le nom et les logos ArtCraft sont des marques de l'équipe ArtCraft et ne sont pas utilisés
par Kazo Pdf.
