using ClassBook.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassBook.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260521113000_NormalizePrimaryScheduleSubjects")]
    public partial class NormalizePrimaryScheduleSubjects : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SET NOCOUNT ON;
                SET QUOTED_IDENTIFIER ON;
                SET ANSI_NULLS ON;

                IF OBJECT_ID('tempdb..#SubjectNames') IS NOT NULL DROP TABLE #SubjectNames;
                CREATE TABLE #SubjectNames
                (
                    SubjectId int NOT NULL,
                    CanonicalName nvarchar(100) NOT NULL
                );

                INSERT INTO #SubjectNames (SubjectId, CanonicalName)
                SELECT
                    SubjectId,
                    CASE
                        WHEN LOWER(LTRIM(RTRIM([Name]))) IN (N'мат-ка', N'математика') THEN N'Математика'
                        WHEN LOWER(LTRIM(RTRIM([Name]))) IN (N'рус яз', N'русс яз', N'русский язык', N'письмо') THEN N'Русский язык'
                        WHEN LOWER(LTRIM(RTRIM([Name]))) IN (N'окр мир', N'окружающий мир') THEN N'Окружающий мир'
                        WHEN LOWER(LTRIM(RTRIM([Name]))) IN (N'чтение', N'лит чтение', N'лит-ра') THEN N'Литературное чтение'
                        WHEN LOWER(LTRIM(RTRIM([Name]))) IN (N'труд', N'технология') THEN N'Труд'
                        WHEN LOWER(LTRIM(RTRIM([Name]))) IN (N'анг яз') THEN N'Английский язык'
                        WHEN LOWER(LTRIM(RTRIM([Name]))) IN (N'кит яз') THEN N'Китайский язык'
                        WHEN LOWER(LTRIM(RTRIM([Name]))) IN (N'физ-ра') THEN N'Физическая культура'
                        ELSE LTRIM(RTRIM([Name]))
                    END
                FROM Subjects
                WHERE LOWER(LTRIM(RTRIM([Name]))) IN
                    (N'мат-ка', N'математика', N'рус яз', N'русс яз', N'русский язык', N'письмо',
                     N'окр мир', N'окружающий мир', N'чтение', N'лит чтение', N'лит-ра',
                     N'труд', N'технология', N'анг яз', N'кит яз', N'физ-ра');

                INSERT INTO Subjects ([Name], TeacherId)
                SELECT sn.CanonicalName, MIN(s.TeacherId)
                FROM #SubjectNames sn
                JOIN Subjects s ON s.SubjectId = sn.SubjectId
                WHERE NOT EXISTS (
                    SELECT 1 FROM Subjects existing
                    WHERE LOWER(LTRIM(RTRIM(existing.[Name]))) = LOWER(sn.CanonicalName)
                )
                GROUP BY sn.CanonicalName;

                IF OBJECT_ID('tempdb..#SubjectTargets') IS NOT NULL DROP TABLE #SubjectTargets;
                CREATE TABLE #SubjectTargets
                (
                    SubjectId int NOT NULL,
                    TargetSubjectId int NOT NULL,
                    CanonicalName nvarchar(100) NOT NULL
                );

                INSERT INTO #SubjectTargets (SubjectId, TargetSubjectId, CanonicalName)
                SELECT sn.SubjectId, target.SubjectId, sn.CanonicalName
                FROM #SubjectNames sn
                CROSS APPLY (
                    SELECT TOP(1) s.SubjectId
                    FROM Subjects s
                    WHERE LOWER(LTRIM(RTRIM(s.[Name]))) = LOWER(sn.CanonicalName)
                    ORDER BY CASE WHEN s.[Name] = sn.CanonicalName THEN 0 ELSE 1 END, s.SubjectId
                ) target;

                UPDATE lesson
                SET SubjectId = target.TargetSubjectId
                FROM Lessons lesson
                JOIN #SubjectTargets target ON target.SubjectId = lesson.SubjectId
                WHERE lesson.SubjectId <> target.TargetSubjectId;

                INSERT INTO SubjectClassAssignments (SubjectId, ClassId, TeacherId, CreatedAt)
                SELECT target.TargetSubjectId, assignment.ClassId, assignment.TeacherId, SYSUTCDATETIME()
                FROM SubjectClassAssignments assignment
                JOIN #SubjectTargets target ON target.SubjectId = assignment.SubjectId
                WHERE NOT EXISTS (
                    SELECT 1 FROM SubjectClassAssignments existing
                    WHERE existing.SubjectId = target.TargetSubjectId
                      AND existing.ClassId = assignment.ClassId
                      AND existing.TeacherId = assignment.TeacherId
                )
                GROUP BY target.TargetSubjectId, assignment.ClassId, assignment.TeacherId;

                DELETE assignment
                FROM SubjectClassAssignments assignment
                JOIN #SubjectTargets target ON target.SubjectId = assignment.SubjectId
                WHERE assignment.SubjectId <> target.TargetSubjectId;

                IF OBJECT_ID('tempdb..#PrimaryMap') IS NOT NULL DROP TABLE #PrimaryMap;
                CREATE TABLE #PrimaryMap
                (
                    SubjectClassAssignmentId int NOT NULL,
                    TargetTeacherId int NOT NULL
                );

                INSERT INTO #PrimaryMap (SubjectClassAssignmentId, TargetTeacherId)
                SELECT assignment.SubjectClassAssignmentId,
                       CASE
                           WHEN target.CanonicalName IN (N'Английский язык', N'ИЗО', N'Китайский язык', N'Физическая культура')
                               THEN assignment.TeacherId
                           ELSE COALESCE(classTeacher.TeacherId, assignment.TeacherId)
                       END
                FROM SubjectClassAssignments assignment
                JOIN Classes classItem ON classItem.ClassId = assignment.ClassId
                JOIN Subjects subject ON subject.SubjectId = assignment.SubjectId
                CROSS APPLY (
                    SELECT
                        CASE
                            WHEN LOWER(LTRIM(RTRIM(subject.[Name]))) IN (N'анг яз') THEN N'Английский язык'
                            WHEN LOWER(LTRIM(RTRIM(subject.[Name]))) IN (N'кит яз') THEN N'Китайский язык'
                            WHEN LOWER(LTRIM(RTRIM(subject.[Name]))) IN (N'физ-ра') THEN N'Физическая культура'
                            ELSE LTRIM(RTRIM(subject.[Name]))
                        END AS CanonicalName
                ) target
                OUTER APPLY (
                    SELECT TOP(1) ct.TeacherId
                    FROM ClassTeachers ct
                    WHERE ct.ClassId = assignment.ClassId
                    ORDER BY ct.ClassTeacherId
                ) classTeacher
                WHERE TRY_CONVERT(int, classItem.[Name]) BETWEEN 0 AND 4
                   OR classItem.[Name] IN (N'1А', N'1Б');

                INSERT INTO SubjectClassAssignments (SubjectId, ClassId, TeacherId, CreatedAt)
                SELECT assignment.SubjectId, assignment.ClassId, map.TargetTeacherId, SYSUTCDATETIME()
                FROM SubjectClassAssignments assignment
                JOIN #PrimaryMap map ON map.SubjectClassAssignmentId = assignment.SubjectClassAssignmentId
                WHERE assignment.TeacherId <> map.TargetTeacherId
                  AND NOT EXISTS (
                      SELECT 1 FROM SubjectClassAssignments duplicate
                      WHERE duplicate.SubjectId = assignment.SubjectId
                        AND duplicate.ClassId = assignment.ClassId
                        AND duplicate.TeacherId = map.TargetTeacherId
                  )
                GROUP BY assignment.SubjectId, assignment.ClassId, map.TargetTeacherId;

                DELETE assignment
                FROM SubjectClassAssignments assignment
                JOIN #PrimaryMap map ON map.SubjectClassAssignmentId = assignment.SubjectClassAssignmentId
                JOIN Subjects subject ON subject.SubjectId = assignment.SubjectId
                WHERE assignment.TeacherId <> map.TargetTeacherId
                  AND subject.[Name] NOT IN (N'Английский язык', N'ИЗО', N'Китайский язык', N'Физическая культура')
                  AND EXISTS (
                      SELECT 1 FROM SubjectClassAssignments duplicate
                      WHERE duplicate.SubjectId = assignment.SubjectId
                        AND duplicate.ClassId = assignment.ClassId
                        AND duplicate.TeacherId = map.TargetTeacherId
                  );

                UPDATE lesson
                SET TeacherId = COALESCE(classTeacher.TeacherId, lesson.TeacherId)
                FROM Lessons lesson
                JOIN Classes classItem ON classItem.ClassId = lesson.ClassId
                JOIN Subjects subject ON subject.SubjectId = lesson.SubjectId
                OUTER APPLY (
                    SELECT TOP(1) ct.TeacherId
                    FROM ClassTeachers ct
                    WHERE ct.ClassId = lesson.ClassId
                    ORDER BY ct.ClassTeacherId
                ) classTeacher
                WHERE (TRY_CONVERT(int, classItem.[Name]) BETWEEN 0 AND 4 OR classItem.[Name] IN (N'1А', N'1Б'))
                  AND subject.[Name] NOT IN (N'Английский язык', N'ИЗО', N'Китайский язык', N'Физическая культура')
                  AND classTeacher.TeacherId IS NOT NULL;

                UPDATE subject
                SET [Name] = target.CanonicalName
                FROM Subjects subject
                JOIN #SubjectTargets target ON target.TargetSubjectId = subject.SubjectId
                WHERE subject.[Name] <> target.CanonicalName;

                DELETE subject
                FROM Subjects subject
                WHERE NOT EXISTS (SELECT 1 FROM Lessons lesson WHERE lesson.SubjectId = subject.SubjectId)
                  AND NOT EXISTS (SELECT 1 FROM SubjectClassAssignments assignment WHERE assignment.SubjectId = subject.SubjectId)
                  AND EXISTS (SELECT 1 FROM #SubjectTargets target WHERE target.SubjectId = subject.SubjectId AND target.TargetSubjectId <> subject.SubjectId);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
