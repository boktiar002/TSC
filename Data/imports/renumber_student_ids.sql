-- Student IDs become TSC + class (2 digits) + roll (2 digits), so TSC0317 is class 3 roll 17.
-- Class 3 was imported as TSC0001-TSC0064 when it was the only class in the system; rolls
-- restart at 1 in every class, so the class has to be part of the ID.
--
-- Nothing references "StudentId" as a foreign key (marks, attendance and payments all point
-- at Students.Id), so this is a rename and nothing else.
--
-- Run:  PGCLIENTENCODING=UTF8 psql -h localhost -U postgres -d coaching_center -f this.sql

BEGIN;

-- Class 3: TSC00NN -> TSC03NN, keeping the roll in the last two digits.
UPDATE "Students"
SET "StudentId" = 'TSC03' || substr("StudentId", 6, 2)
WHERE "StudentId" ~ '^TSC00[0-9]{2}$';

-- The lone pre-existing record, class 5.
UPDATE "Students"
SET "StudentId" = 'TSC0501'
WHERE "StudentId" = 'ST001';

COMMIT;
