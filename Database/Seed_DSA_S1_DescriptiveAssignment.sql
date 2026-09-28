SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.SessionDescriptiveAssignments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SessionDescriptiveAssignments
    (
        SessionDescriptiveAssignmentId INT IDENTITY(1,1) PRIMARY KEY,
        SessionId NVARCHAR(30) NOT NULL,
        ItemLabel NVARCHAR(100) NOT NULL,
        QuestionsJson NVARCHAR(MAX) NOT NULL,
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_SessionDescriptiveAssignments_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_SessionDescriptiveAssignments UNIQUE(SessionId, ItemLabel)
    );
END;
GO

DECLARE @Questions TABLE
(
    DisplayOrder INT,
    QuestionLabel NVARCHAR(50),
    AnswerLabel NVARCHAR(80),
    QuestionText NVARCHAR(MAX),
    AnswerText NVARCHAR(MAX),
    TestAnswerText NVARCHAR(MAX)
);

INSERT INTO @Questions VALUES
(1, N'Question-1', N'Answer-1 (Reference Answer Key)',
 N'Explain the difference between JDK, JRE, and JVM, and how Java achieves platform independence.',
 N'JVM (Java Virtual Machine): Executes Java bytecode. It is platform-specific but provides a uniform runtime environment.

JRE (Java Runtime Environment): Contains the JVM and core libraries needed to run Java programs.

JDK (Java Development Kit): Contains the JRE and development tools, such as the javac compiler, needed to write and compile Java programs.

Platform independence: Java source code is compiled into platform-independent bytecode (.class files). This bytecode can run on any operating system that has a compatible JVM installed—commonly described as "Write Once, Run Anywhere."', N''),
(2, N'Question-2', N'Answer-2 (Reference Answer Key)',
 N'What is a keyword in Java? Give five examples and list the rules for naming valid identifiers.',
 N'A keyword is a reserved word with a predefined meaning in Java. It cannot be used as the name of a variable, method, or class.

Examples: class, public, static, void, and if.

Identifier rules:
• An identifier can contain letters, digits, underscores (_), and dollar signs ($).
• It must not begin with a digit.
• It cannot be a Java keyword.
• Identifiers are case-sensitive; for example, age and Age are different.
• Spaces and special characters such as @, -, and % are not allowed.', N''),
(3, N'Question-3', N'Answer-3 (Reference Answer Key)',
 N'List the categories of operators in Java and explain operator precedence with an example.',
 N'The main categories are arithmetic, relational, logical, assignment, unary, bitwise, and ternary operators.

Operator precedence determines the order in which operators are evaluated when an expression contains multiple operators. For example:

int result = 5 + 2 * 3;

Multiplication (*) has higher precedence than addition (+), so 2 * 3 is evaluated first to produce 6. Then 5 + 6 produces 11, not 21.', N''),
(4, N'Question-4', N'Answer-4 (Reference Answer Key)',
 N'Differentiate between implicit and explicit type conversion. Why does converting a double to an int cause data loss?',
 N'Implicit (widening) conversion happens automatically when a smaller data type is converted to a larger compatible type, such as int to double, because the value can be represented without losing information.

Explicit (narrowing) conversion requires a manual cast, such as (int) myDouble, when converting a larger type to a smaller type because information may be lost.

Converting double to int loses data because int cannot store a fractional component. Java truncates the fractional part, so (int) 9.99 becomes 9.', N''),
(5, N'Question-5', N'Answer-5 (Reference Answer Key)',
 N'Explain Java control structures. Differentiate between while and do-while loops.',
 N'Control structures determine the flow of execution in a program. They include decision-making structures such as if, if-else, and switch, and looping structures such as for, while, and do-while.

A while loop checks its condition before executing the loop body. If the condition is initially false, the body does not execute.

A do-while loop executes the body first and checks its condition afterward. Therefore, its body is guaranteed to execute at least once.', N''),
(6, N'Question-6', N'Answer-6 (Reference Answer Key)',
 N'What is Object-Oriented Programming (OOP)? Explain the relationship between a class and an object with an analogy.',
 N'Object-Oriented Programming is a programming paradigm based on objects that combine data (fields) and behavior (methods). Its four main principles are encapsulation, abstraction, inheritance, and polymorphism.

A class is like a house blueprint: it defines the structure but is not itself a physical house. An object is an actual house created from that blueprint. Multiple distinct objects can be created from the same class.', N''),
(7, N'Question-7', N'Answer-7 (Reference Answer Key)',
 N'What is a method in Java? Explain method overloading with an example.',
 N'A method is a block of code inside a class that performs a specific task.

Method overloading means defining multiple methods in the same class with the same name but different parameter lists. The parameters can differ in number, type, or order. For example:

void add(int a, int b) { }
void add(double a, double b) { }

Java selects the appropriate method at compile time according to the supplied arguments. This is compile-time polymorphism.', N''),
(8, N'Question-8', N'Answer-8 (Reference Answer Key)',
 N'Differentiate between a default constructor and a parameterized constructor. What is constructor overloading?',
 N'A default constructor takes no arguments. Java automatically provides one when a class declares no constructor, and it initializes fields to their default values.

A parameterized constructor accepts arguments and uses them to initialize an object''s fields with specific values during creation.

Constructor overloading means defining multiple constructors in the same class with different parameter lists, allowing objects to be initialized in different ways.', N''),
(9, N'Question-9', N'Answer-9 (Reference Answer Key)',
 N'Explain the purpose of the this keyword in Java.',
 N'The this keyword refers to the current object instance.

It resolves naming conflicts by distinguishing instance variables from method or constructor parameters with the same name, as in this.name = name.

It also supports constructor chaining by calling another constructor in the same class, such as this("Unknown"). Such a call must be the first statement in the constructor.', N''),
(10, N'Question-10', N'Answer-10 (Reference Answer Key)',
 N'Explain the difference between stack and heap memory. What is garbage collection?',
 N'Stack memory stores method calls, local variables, and object references. It follows last-in, first-out behavior and is cleared automatically when methods finish.

Heap memory stores the actual objects created with the new keyword and is shared across the application.

Garbage collection is the JVM''s automatic process for reclaiming heap memory occupied by objects that are no longer reachable. It reduces memory leaks and removes the need for manual memory deallocation.', N'');

DECLARE @QuestionsJson NVARCHAR(MAX) =
(
    SELECT QuestionLabel AS questionLabel,
           AnswerLabel AS answerLabel,
           QuestionText AS questionText,
           AnswerText AS answerText,
           TestAnswerText AS testAnswerText
    FROM @Questions
    ORDER BY DisplayOrder
    FOR JSON PATH
);

MERGE dbo.SessionDescriptiveAssignments AS target
USING (SELECT N'DSA-S1' SessionId, N'Descriptive Assignment-1' ItemLabel) AS source
ON target.SessionId = source.SessionId AND target.ItemLabel = source.ItemLabel
WHEN MATCHED THEN UPDATE SET QuestionsJson = @QuestionsJson, UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (SessionId, ItemLabel, QuestionsJson, UpdatedAt)
VALUES (source.SessionId, source.ItemLabel, @QuestionsJson, SYSUTCDATETIME());

SELECT SessionId, ItemLabel,
       (SELECT COUNT(*) FROM OPENJSON(QuestionsJson)) QuestionCount,
       LEN(QuestionsJson) JsonLength
FROM dbo.SessionDescriptiveAssignments
WHERE SessionId = N'DSA-S1' AND ItemLabel = N'Descriptive Assignment-1';
