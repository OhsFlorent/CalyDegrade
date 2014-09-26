CREATE TABLE `base_bainville_mots` (
	`num_mot`	INTEGER NOT NULL,
	`ipp_adm`	INTEGER NOT NULL,
	`nomm`	TEXT NOT NULL,
	`prenom`	TEXT NOT NULL,
	`type_mot`	TEXT NOT NULL,
	`resume_mot`	TEXT NOT NULL,
	`date`	TEXT NOT NULL,
	`nom_pre`	TEXT NOT NULL,
	`text`	INTEGER NOT NULL,
	`file_name`	TEXT NOT NULL,
	`file_last_modif`	INTEGER NOT NULL,
	PRIMARY KEY(num_mot)
);

CREATE TABLE `base_flavigny_mots` (
	`num_mot`	INTEGER NOT NULL,
	`ipp_adm`	INTEGER NOT NULL,
	`nomm`	TEXT NOT NULL,
	`prenom`	TEXT NOT NULL,
	`type_mot`	TEXT NOT NULL,
	`resume_mot`	TEXT NOT NULL,
	`date`	TEXT NOT NULL,
	`nom_pre`	TEXT NOT NULL,
	`text`	INTEGER NOT NULL,
	`file_name`	TEXT NOT NULL,
	`file_last_modif`	INTEGER NOT NULL,
	PRIMARY KEY(num_mot)
);