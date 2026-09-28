SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.SessionProgrammingExercises', N'U') IS NULL
    THROW 50001, 'SessionProgrammingExercises must exist before seeding assignments.', 1;

IF COL_LENGTH('dbo.SessionProgrammingExercises', 'StarterCode') IS NULL
    ALTER TABLE dbo.SessionProgrammingExercises ADD StarterCode NVARCHAR(MAX) NOT NULL CONSTRAINT DF_SessionProgrammingExercises_StarterCode DEFAULT '';
GO

DECLARE @Assignments TABLE
(
    ItemLabel NVARCHAR(100),
    Question NVARCHAR(MAX),
    StarterCode NVARCHAR(MAX),
    TestCasesJson NVARCHAR(MAX)
);

INSERT INTO @Assignments VALUES
(N'Programming Assignment-1',
 N'<h2>Assignment 1: Second Largest Element in an Array</h2><h3>Question</h3><p>Implement a program to find the second largest distinct element in an array of integers.</p><h3>Hints</h3><p>Initialize two variables to <code>Integer.MIN_VALUE</code>. Update them in a single pass.</p><h3>Approach</h3><p>If the current value is greater than largest, assign largest to secondLargest and then update largest. If it lies between them and is not equal to largest, update secondLargest.</p>',
 N'import java.util.ArrayList;
import java.util.List;
import java.util.Scanner;

class Tester {
    public static void main(String[] args) {
        Scanner sc = new Scanner(System.in);
        String input = sc.hasNextLine() ? sc.nextLine() : "";
        String[] parts = input.trim().split("[,\\s]+" );
        List<Integer> values = new ArrayList<>();
        for (String part : parts) {
            if (!part.isBlank()) values.add(Integer.parseInt(part));
        }

        int largest = Integer.MIN_VALUE;
        int secondLargest = Integer.MIN_VALUE;

        for (int n : values) {
            // TODO: Update largest and secondLargest appropriately
            // Remember to handle duplicates!
        }
        System.out.println(secondLargest);
    }
}',
 N'[{"testCaseId":"TC-1","input":"12, 45, 2, 41, 31","output":"41"},{"testCaseId":"TC-2","input":"8, 8, 8, 3","output":"3"},{"testCaseId":"TC-3","input":"1, 2, 3, 4, 5","output":"4"},{"testCaseId":"TC-4","input":"5, 4, 3, 2, 1","output":"4"},{"testCaseId":"TC-5","input":"10, 10, 9, 9","output":"9"}]'),
(N'Programming Assignment-2',
 N'<h2>Assignment 2: Check if a String is a Palindrome</h2><h3>Question</h3><p>Implement a program that checks whether a string is a palindrome, ignoring case.</p><h3>Hints</h3><p>Convert the string to lowercase before comparing. Use StringBuilder to reverse it.</p><h3>Approach</h3><p>Convert the input to lowercase, create a reversed version using <code>StringBuilder.reverse()</code>, and compare the two strings using <code>equals()</code>.</p>',
 N'import java.util.Scanner;

class Tester {
    public static String isPalindrome(String str) {
        // TODO: Convert to lowercase

        // TODO: Reverse the string using StringBuilder

        // TODO: Compare and return "true" or "false" as a String
        return "false";
    }

    public static void main(String[] args) {
        Scanner sc = new Scanner(System.in);
        String input = sc.hasNextLine() ? sc.nextLine() : "";
        System.out.println(isPalindrome(input));
    }
}',
 N'[{"testCaseId":"TC-1","input":"Madam","output":"true"},{"testCaseId":"TC-2","input":"Hello World","output":"false"},{"testCaseId":"TC-3","input":"Racecar","output":"true"},{"testCaseId":"TC-4","input":"Level","output":"true"},{"testCaseId":"TC-5","input":"Java","output":"false"}]'),
(N'Programming Assignment-3',
 N'<h2>Assignment 3: Employee Class (Constructor Overloading)</h2><h3>Question</h3><p>Implement a class <code>Employee</code>. Provide two constructors: one taking name and salary, which defaults the bonus to 5000 using constructor chaining, and another taking all three values.</p><h3>Hints</h3><p>Use <code>this(name, salary, 5000)</code> in the two-argument constructor.</p><h3>Approach</h3><p>Create a three-argument constructor that initializes every field. Create a two-argument constructor that invokes it using <code>this()</code>.</p>',
 N'import java.util.Scanner;

class Employee {
    String name;
    double salary;
    double bonus;

    Employee(String name, double salary) {
        // TODO: Chain to the 3-argument constructor with a bonus of 5000
    }

    Employee(String name, double salary, double bonus) {
        this.name = name;
        this.salary = salary;
        this.bonus = bonus;
    }

    void display() {
        System.out.println("Name: " + name + ", Salary: " + salary + ", Bonus: " + bonus);
    }
}

class Tester {
    public static void main(String[] args) {
        Scanner sc = new Scanner(System.in);
        String[] parts = sc.nextLine().replace("\\\"", "").split("\\s*,\\s*");
        Employee employee = parts.length >= 3
            ? new Employee(parts[0], Double.parseDouble(parts[1]), Double.parseDouble(parts[2]))
            : new Employee(parts[0], Double.parseDouble(parts[1]));
        employee.display();
    }
}',
 N'[{"testCaseId":"TC-1","input":"Alice, 50000","output":"Name: Alice, Salary: 50000.0, Bonus: 5000.0"},{"testCaseId":"TC-2","input":"Bob, 60000, 8000","output":"Name: Bob, Salary: 60000.0, Bonus: 8000.0"},{"testCaseId":"TC-3","input":"Charlie, 45000","output":"Name: Charlie, Salary: 45000.0, Bonus: 5000.0"},{"testCaseId":"TC-4","input":"Diana, 70000, 10000","output":"Name: Diana, Salary: 70000.0, Bonus: 10000.0"},{"testCaseId":"TC-5","input":"Eve, 0, 0","output":"Name: Eve, Salary: 0.0, Bonus: 0.0"}]'),
(N'Programming Assignment-4',
 N'<h2>Assignment 4: Shape Area (Inheritance &amp; Polymorphism)</h2><h3>Question</h3><p>Implement a superclass <code>Shape</code> with an <code>area()</code> method, and two subclasses, <code>Circle</code> and <code>Rectangle</code>, that override it.</p><h3>Hints</h3><p>Use <code>Math.PI</code> for the circle and override <code>area()</code> in both subclasses.</p><h3>Approach</h3><p>Define Shape with a default area. Extend it with Circle (πr²) and Rectangle (length × width). Print both resulting areas.</p>',
 N'import java.util.Scanner;

class Shape {
    double area() { return 0; }
}

class Circle extends Shape {
    double radius;
    Circle(double radius) { this.radius = radius; }

    @Override
    double area() {
        // TODO: Calculate and return circle area rounded to 2 decimal places
        return 0;
    }
}

class Rectangle extends Shape {
    double length, width;
    Rectangle(double length, double width) {
        this.length = length;
        this.width = width;
    }

    @Override
    double area() {
        // TODO: Calculate and return rectangle area
        return 0;
    }
}

class Tester {
    public static void main(String[] args) {
        Scanner sc = new Scanner(System.in);
        double radius = sc.nextDouble();
        double length = sc.nextDouble();
        double width = sc.nextDouble();
        Shape[] shapes = { new Circle(radius), new Rectangle(length, width) };
        for (Shape shape : shapes) System.out.println(shape.area());
    }
}',
 N'[{"testCaseId":"TC-1","input":"5 4 6","output":"78.54\n24.0"},{"testCaseId":"TC-2","input":"1 10 10","output":"3.14\n100.0"},{"testCaseId":"TC-3","input":"0 5 5","output":"0.0\n25.0"},{"testCaseId":"TC-4","input":"2 3 4","output":"12.57\n12.0"},{"testCaseId":"TC-5","input":"10 1 1","output":"314.16\n1.0"}]'),
(N'Programming Assignment-5',
 N'<h2>Assignment 5: Student Roll Number Generator (Static Counter)</h2><h3>Question</h3><p>Implement a class <code>Student</code> that automatically generates roll numbers starting at S1001 and increments by one for each new object, using a static counter initialized in a static block.</p><h3>Hints</h3><p>Use a static integer variable and initialize it inside a <code>static { ... }</code> block.</p><h3>Approach</h3><p>Set counter to 1001 in the static block. In the constructor, assign <code>rollNo = "S" + counter</code>, then increment the counter.</p>',
 N'import java.util.Scanner;

class Student {
    private static int counter;
    private String rollNo;
    private String name;

    static {
        // TODO: Initialize the static counter to 1001
    }

    public Student(String name) {
        this.name = name;
        // TODO: Assign rollNo using the counter, then increment the counter
    }

    public String getRollNo() { return rollNo; }
    public String getName() { return name; }
}

class Tester {
    public static void main(String[] args) {
        Scanner sc = new Scanner(System.in);
        String input = sc.hasNextLine() ? sc.nextLine() : "";
        String[] names = input.replace("\\\"", "").split("\\s*,\\s*");
        for (String name : names) {
            if (name.isBlank()) continue;
            Student student = new Student(name.trim());
            System.out.println("Roll No: " + student.getRollNo() + ", Name: " + student.getName());
        }
    }
}',
 N'[{"testCaseId":"TC-1","input":"John, Priya","output":"Roll No: S1001, Name: John\nRoll No: S1002, Name: Priya"},{"testCaseId":"TC-2","input":"A, B, C","output":"Roll No: S1001, Name: A\nRoll No: S1002, Name: B\nRoll No: S1003, Name: C"},{"testCaseId":"TC-3","input":"Alice","output":"Roll No: S1001, Name: Alice"},{"testCaseId":"TC-4","input":"W, X, Y, Z","output":"Roll No: S1001, Name: W\nRoll No: S1002, Name: X\nRoll No: S1003, Name: Y\nRoll No: S1004, Name: Z"},{"testCaseId":"TC-5","input":"Sam, Sam","output":"Roll No: S1001, Name: Sam\nRoll No: S1002, Name: Sam"}]');

MERGE dbo.SessionProgrammingExercises AS target
USING (SELECT N'DSA-S1' SessionId, ItemLabel, Question, StarterCode, TestCasesJson FROM @Assignments) AS source
ON target.SessionId = source.SessionId AND target.ItemLabel = source.ItemLabel
WHEN MATCHED THEN UPDATE SET
    Question = source.Question,
    StarterCode = source.StarterCode,
    TestCasesJson = source.TestCasesJson,
    UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (SessionId, ItemLabel, Question, StarterCode, TestCasesJson, UpdatedAt)
VALUES (source.SessionId, source.ItemLabel, source.Question, source.StarterCode, source.TestCasesJson, SYSUTCDATETIME());

SELECT SessionId, ItemLabel, LEN(Question) QuestionLength, LEN(StarterCode) StarterCodeLength,
       (SELECT COUNT(*) FROM OPENJSON(TestCasesJson)) TestCaseCount
FROM dbo.SessionProgrammingExercises
WHERE SessionId = N'DSA-S1' AND ItemLabel LIKE N'Programming Assignment-%'
ORDER BY ItemLabel;
