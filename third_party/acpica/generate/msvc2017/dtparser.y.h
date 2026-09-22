/* A Bison parser, made by GNU Bison 3.8.2.  */

/* Bison interface for Yacc-like parsers in C

   Copyright (C) 1984, 1989-1990, 2000-2015, 2018-2021 Free Software Foundation,
   Inc.

   This program is free software: you can redistribute it and/or modify
   it under the terms of the GNU General Public License as published by
   the Free Software Foundation, either version 3 of the License, or
   (at your option) any later version.

   This program is distributed in the hope that it will be useful,
   but WITHOUT ANY WARRANTY; without even the implied warranty of
   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
   GNU General Public License for more details.

   You should have received a copy of the GNU General Public License
   along with this program.  If not, see <https://www.gnu.org/licenses/>.  */

/* As a special exception, you may create a larger work that contains
   part or all of the Bison parser skeleton and distribute that work
   under terms of your choice, so long as that work isn't itself a
   parser generator using the skeleton or a modified version thereof
   as a parser skeleton.  Alternatively, if you modify or redistribute
   the parser skeleton itself, you may (at your option) remove this
   special exception, which will cause the skeleton and the resulting
   Bison output files to be licensed under the GNU General Public
   License without this special exception.

   This special exception was added by the Free Software Foundation in
   version 2.2 of Bison.  */

/* DO NOT RELY ON FEATURES THAT ARE NOT DOCUMENTED in the manual,
   especially those whose name start with YY_ or yy_.  They are
   private implementation details that can be changed or removed.  */

#ifndef YY_DTPARSER_DTPARSER_Y_H_INCLUDED
# define YY_DTPARSER_DTPARSER_Y_H_INCLUDED
/* Debug traces.  */
#ifndef YYDEBUG
# define YYDEBUG 0
#endif
#if YYDEBUG
extern int DtParserdebug;
#endif

/* Token kinds.  */
#ifndef YYTOKENTYPE
# define YYTOKENTYPE
  enum yytokentype
  {
    YYEMPTY = -2,
    YYEOF = 0,                     /* "end of file"  */
    YYerror = 256,                 /* error  */
    YYUNDEF = 257,                 /* "invalid token"  */
    OP_EXP_EOF = 258,              /* OP_EXP_EOF  */
    OP_EXP_NEW_LINE = 259,         /* OP_EXP_NEW_LINE  */
    OP_EXP_NUMBER = 260,           /* OP_EXP_NUMBER  */
    OP_EXP_HEX_NUMBER = 261,       /* OP_EXP_HEX_NUMBER  */
    OP_EXP_DECIMAL_NUMBER = 262,   /* OP_EXP_DECIMAL_NUMBER  */
    OP_EXP_LABEL = 263,            /* OP_EXP_LABEL  */
    OP_EXP_PAREN_OPEN = 264,       /* OP_EXP_PAREN_OPEN  */
    OP_EXP_PAREN_CLOSE = 265,      /* OP_EXP_PAREN_CLOSE  */
    OP_EXP_LOGICAL_OR = 266,       /* OP_EXP_LOGICAL_OR  */
    OP_EXP_LOGICAL_AND = 267,      /* OP_EXP_LOGICAL_AND  */
    OP_EXP_OR = 268,               /* OP_EXP_OR  */
    OP_EXP_XOR = 269,              /* OP_EXP_XOR  */
    OP_EXP_AND = 270,              /* OP_EXP_AND  */
    OP_EXP_EQUAL = 271,            /* OP_EXP_EQUAL  */
    OP_EXP_NOT_EQUAL = 272,        /* OP_EXP_NOT_EQUAL  */
    OP_EXP_GREATER = 273,          /* OP_EXP_GREATER  */
    OP_EXP_LESS = 274,             /* OP_EXP_LESS  */
    OP_EXP_GREATER_EQUAL = 275,    /* OP_EXP_GREATER_EQUAL  */
    OP_EXP_LESS_EQUAL = 276,       /* OP_EXP_LESS_EQUAL  */
    OP_EXP_SHIFT_RIGHT = 277,      /* OP_EXP_SHIFT_RIGHT  */
    OP_EXP_SHIFT_LEFT = 278,       /* OP_EXP_SHIFT_LEFT  */
    OP_EXP_ADD = 279,              /* OP_EXP_ADD  */
    OP_EXP_SUBTRACT = 280,         /* OP_EXP_SUBTRACT  */
    OP_EXP_MULTIPLY = 281,         /* OP_EXP_MULTIPLY  */
    OP_EXP_DIVIDE = 282,           /* OP_EXP_DIVIDE  */
    OP_EXP_MODULO = 283,           /* OP_EXP_MODULO  */
    OP_EXP_ONES_COMPLIMENT = 284,  /* OP_EXP_ONES_COMPLIMENT  */
    OP_EXP_LOGICAL_NOT = 285       /* OP_EXP_LOGICAL_NOT  */
  };
  typedef enum yytokentype yytoken_kind_t;
#endif

/* Value type.  */
#if ! defined YYSTYPE && ! defined YYSTYPE_IS_DECLARED
union YYSTYPE
{

     UINT64                 value;
     UINT32                 op;


};
typedef union YYSTYPE YYSTYPE;
# define YYSTYPE_IS_TRIVIAL 1
# define YYSTYPE_IS_DECLARED 1
#endif


extern YYSTYPE DtParserlval;


int DtParserparse (void);


#endif /* !YY_DTPARSER_DTPARSER_Y_H_INCLUDED  */
