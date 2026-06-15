SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @RussianSubjectId int = 38;
DECLARE @PetrovaId int = 109;
DECLARE @MiddleTeacherId int = 92;
DECLARE @SeniorTeacherId int = 114;
DECLARE @QuarterStart date = '2026-03-30';

DECLARE @PrimaryClasses table (ClassId int primary key, LessonNumber int not null);
INSERT INTO @PrimaryClasses (ClassId, LessonNumber)
VALUES (2, 1), (3, 2), (4, 3), (5, 4);

DECLARE @TargetTeachers table (ClassId int primary key, TeacherId int not null);
INSERT INTO @TargetTeachers (ClassId, TeacherId)
SELECT ClassId,
       CASE WHEN ClassId BETWEEN 6 AND 9 THEN @MiddleTeacherId ELSE @SeniorTeacherId END
FROM dbo.Classes
WHERE ClassId NOT IN (SELECT ClassId FROM @PrimaryClasses);

UPDATE assignment
SET TeacherId = @PetrovaId
FROM dbo.SubjectClassAssignments assignment
JOIN @PrimaryClasses primaryClass ON primaryClass.ClassId = assignment.ClassId
WHERE assignment.SubjectId = @RussianSubjectId;

INSERT INTO dbo.SubjectClassAssignments (SubjectId, ClassId, TeacherId, CreatedAt)
SELECT @RussianSubjectId, primaryClass.ClassId, @PetrovaId, SYSUTCDATETIME()
FROM @PrimaryClasses primaryClass
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.SubjectClassAssignments assignment
    WHERE assignment.SubjectId = @RussianSubjectId
      AND assignment.ClassId = primaryClass.ClassId
      AND assignment.TeacherId = @PetrovaId
);

INSERT INTO dbo.SubjectClassAssignments (SubjectId, ClassId, TeacherId, CreatedAt)
SELECT @RussianSubjectId, target.ClassId, target.TeacherId, SYSUTCDATETIME()
FROM @TargetTeachers target
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.SubjectClassAssignments assignment
    WHERE assignment.SubjectId = @RussianSubjectId
      AND assignment.ClassId = target.ClassId
      AND assignment.TeacherId = target.TeacherId
);

DELETE assignment
FROM dbo.SubjectClassAssignments assignment
WHERE assignment.SubjectId = @RussianSubjectId
  AND assignment.TeacherId = @PetrovaId
  AND assignment.ClassId NOT IN (SELECT ClassId FROM @PrimaryClasses);

UPDATE lesson
SET TeacherId = target.TeacherId
FROM dbo.Lessons lesson
JOIN @TargetTeachers target ON target.ClassId = lesson.ClassId
WHERE lesson.SubjectId = @RussianSubjectId
  AND lesson.TeacherId = @PetrovaId;

;WITH numbered AS (
    SELECT lesson.LessonId,
           lesson.ClassId,
           primaryClass.LessonNumber,
           ROW_NUMBER() OVER (PARTITION BY lesson.ClassId ORDER BY lesson.Date, lesson.LessonId) AS RowNo
    FROM dbo.Lessons lesson
    JOIN @PrimaryClasses primaryClass ON primaryClass.ClassId = lesson.ClassId
    WHERE lesson.SubjectId = @RussianSubjectId
      AND lesson.TeacherId = @PetrovaId
),
planned AS (
    SELECT numbered.LessonId,
           CASE
               WHEN numbered.RowNo <= 36 THEN DATEADD(day, ((numbered.RowNo - 1) / 4) * 7 + ((numbered.RowNo - 1) % 4), @QuarterStart)
               ELSE DATEADD(day, ((numbered.RowNo - 37) * 7) + 4, @QuarterStart)
           END AS LessonDate,
           CASE
               WHEN numbered.RowNo <= 36 THEN ((numbered.RowNo - 1) % 4) * 7 + numbered.LessonNumber
               ELSE 28 + numbered.LessonNumber
           END AS ScheduleId,
           numbered.RowNo
    FROM numbered
)
UPDATE lesson
SET Date = planned.LessonDate,
    ScheduleId = planned.ScheduleId,
    Topic = CASE planned.RowNo % 6
        WHEN 1 THEN N'Правописание безударных гласных'
        WHEN 2 THEN N'Части речи и их признаки'
        WHEN 3 THEN N'Главные члены предложения'
        WHEN 4 THEN N'Словарные слова'
        WHEN 5 THEN N'Текст и основная мысль'
        ELSE N'Повторение изученного'
    END,
    Homework = CASE planned.RowNo % 6
        WHEN 1 THEN N'Упражнение по теме, подготовить 5 слов'
        WHEN 2 THEN N'Разобрать 3 предложения'
        WHEN 3 THEN N'Составить 4 предложения'
        WHEN 4 THEN N'Выучить словарные слова'
        WHEN 5 THEN N'Написать мини-текст'
        ELSE N'Повторить правила'
    END
FROM dbo.Lessons lesson
JOIN planned ON planned.LessonId = lesson.LessonId;

COMMIT TRANSACTION;
