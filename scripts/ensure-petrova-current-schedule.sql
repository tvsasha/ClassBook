SET NOCOUNT ON;
SET DATEFIRST 7;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;

DECLARE @TeacherId INT = (
    SELECT TOP 1 Id
    FROM Users
    WHERE FullName = N'Петрова Елена Сергеевна'
);

DECLARE @SubjectId INT = (
    SELECT TOP 1 SubjectId
    FROM Subjects
    WHERE Name = N'Русский язык'
    ORDER BY SubjectId
);

IF @TeacherId IS NULL OR @SubjectId IS NULL
BEGIN
    THROW 51000, N'Не найдены Петрова Елена Сергеевна или предмет Русский язык.', 1;
END;

DECLARE @Classes TABLE
(
    RowNumber INT IDENTITY(1,1) PRIMARY KEY,
    ClassId INT NOT NULL,
    ClassName NVARCHAR(50) NOT NULL
);

INSERT INTO @Classes (ClassId, ClassName)
SELECT ClassId, Name
FROM Classes
WHERE Name IN (N'1', N'2', N'3', N'4')
ORDER BY TRY_CONVERT(INT, Name), Name;

DECLARE @Dates TABLE
(
    RowNumber INT IDENTITY(1,1) PRIMARY KEY,
    LessonDate DATE NOT NULL,
    Topic NVARCHAR(100) NOT NULL,
    Homework NVARCHAR(MAX) NULL
);

INSERT INTO @Dates (LessonDate, Topic, Homework)
VALUES
('2026-06-15', N'Повторение орфограмм', N'Упр. 214, повторить правила'),
('2026-06-16', N'Состав слова', N'Разобрать 8 слов по составу'),
('2026-06-17', N'Безударные гласные', N'Упр. 219, подобрать проверочные слова'),
('2026-06-18', N'Парные согласные', N'Упр. 224, словарная работа'),
('2026-06-19', N'Развитие речи', N'Мини-сочинение на 7 предложений'),
('2026-06-22', N'Части речи', N'Упр. 231, таблица частей речи'),
('2026-06-23', N'Имя существительное', N'Упр. 236, определить падеж'),
('2026-06-24', N'Имя прилагательное', N'Упр. 241, согласование слов'),
('2026-06-25', N'Глагол', N'Упр. 247, времена глаголов'),
('2026-06-26', N'Итоговое повторение', N'Подготовиться к проверочной работе');

INSERT INTO SubjectClassAssignments (SubjectId, ClassId, TeacherId, CreatedAt)
SELECT @SubjectId, c.ClassId, @TeacherId, SYSUTCDATETIME()
FROM @Classes c
WHERE NOT EXISTS (
    SELECT 1
    FROM SubjectClassAssignments sca
    WHERE sca.SubjectId = @SubjectId
      AND sca.ClassId = c.ClassId
      AND sca.TeacherId = @TeacherId
);

DECLARE @DateIndex INT = 1;
DECLARE @DateCount INT = (SELECT COUNT(*) FROM @Dates);

WHILE @DateIndex <= @DateCount
BEGIN
    DECLARE @LessonDate DATE;
    DECLARE @Topic NVARCHAR(100);
    DECLARE @Homework NVARCHAR(MAX);

    SELECT
        @LessonDate = LessonDate,
        @Topic = Topic,
        @Homework = Homework
    FROM @Dates
    WHERE RowNumber = @DateIndex;

    DECLARE @ClassIndex INT = 1;
    DECLARE @ClassCount INT = (SELECT COUNT(*) FROM @Classes);

    WHILE @ClassIndex <= @ClassCount
    BEGIN
        DECLARE @ClassId INT;
        SELECT @ClassId = ClassId
        FROM @Classes
        WHERE RowNumber = @ClassIndex;

        IF NOT EXISTS (
            SELECT 1
            FROM Lessons
            WHERE TeacherId = @TeacherId
              AND SubjectId = @SubjectId
              AND ClassId = @ClassId
              AND CAST([Date] AS DATE) = @LessonDate
        )
        BEGIN
            DECLARE @ScheduleId INT = NULL;
            DECLARE @MaxClassLesson INT = ISNULL((
                SELECT MAX(s.LessonNumber)
                FROM Lessons l
                INNER JOIN Schedules s ON s.ScheduleId = l.ScheduleId
                WHERE l.ClassId = @ClassId
                  AND CAST(l.[Date] AS DATE) = @LessonDate
            ), 0);

            SELECT TOP 1 @ScheduleId = s.ScheduleId
            FROM Schedules s
            WHERE s.DayOfWeek = DATEPART(WEEKDAY, @LessonDate) - 2
              AND NOT EXISTS (
                  SELECT 1
                  FROM Lessons l
                  WHERE l.ClassId = @ClassId
                    AND l.ScheduleId = s.ScheduleId
                    AND CAST(l.[Date] AS DATE) = @LessonDate
              )
              AND NOT EXISTS (
                  SELECT 1
                  FROM Lessons l
                  INNER JOIN Schedules usedSchedule ON usedSchedule.ScheduleId = l.ScheduleId
                  WHERE l.TeacherId = @TeacherId
                    AND usedSchedule.LessonNumber = s.LessonNumber
                    AND CAST(l.[Date] AS DATE) = @LessonDate
              )
            ORDER BY
              CASE WHEN s.LessonNumber <= @MaxClassLesson THEN 0 ELSE 1 END,
              s.LessonNumber;

            IF @ScheduleId IS NOT NULL
            BEGIN
                INSERT INTO Lessons (SubjectId, ClassId, TeacherId, ScheduleId, Topic, [Date], Homework)
                VALUES (@SubjectId, @ClassId, @TeacherId, @ScheduleId, @Topic, @LessonDate, @Homework);
            END;
        END;

        SET @ClassIndex += 1;
    END;

    SET @DateIndex += 1;
END;
