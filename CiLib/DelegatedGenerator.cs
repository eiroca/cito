// DelegatedGenerator.cs - Base class for delegate-approach generators
//
// Copyright (C) 2013  Enrico Croce
//
// This file is part of CiTo, see http://cito.sourceforge.net
//
// CiTo is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// CiTo is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with CiTo.  If not, see http://www.gnu.org/licenses/

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Foxoft.Ci {

  public delegate bool StatementActionDelegate(ICiStatement s);
  //
  public delegate void WriteSymbolDelegate(CiSymbol statement);
  public delegate void WriteStatementDelegate(ICiStatement statement);
  public delegate void WriteExprDelegate(CiExpr expr);
  public delegate void WriteUnaryOperatorDelegate(CiUnaryExpr expr, UnaryOperatorInfo token);
  public delegate void WriteBinaryOperatorDelegate(CiBinaryExpr expr, BinaryOperatorInfo token);
  //
  public delegate void WritePropertyAccessDelegate(CiPropertyAccess expr);
  public delegate void WriteMethodDelegate(CiMethodCall method);
  //
  public delegate string TranslateSymbolNameDelegate(CiSymbol aSymbol);
  //
  public class OperatorInfo {
    public CiToken Token;
    public CiPriority Priority;
  }

  public class UnaryOperatorInfo: OperatorInfo {
    public string Prefix;
    public string Suffix;
    public WriteUnaryOperatorDelegate WriteDelegate;

    public UnaryOperatorInfo(CiToken token, CiPriority priority, WriteUnaryOperatorDelegate writeDelegate, string prefix, string suffix) {
      this.Token = token;
      this.Priority = priority;
      this.WriteDelegate = writeDelegate;
      this.Prefix = prefix;
      this.Suffix = suffix;
    }
  }

  public class BinaryOperatorInfo: OperatorInfo {
    public string Symbol;
    public bool Associative;
    public WriteBinaryOperatorDelegate WriteDelegate;

    public BinaryOperatorInfo(CiToken token, CiPriority priority, bool associative, WriteBinaryOperatorDelegate writeDelegate, string symbol) {
      this.Token = token;
      this.Priority = priority;
      this.Associative = associative;
      this.Symbol = symbol;
      this.WriteDelegate = writeDelegate;
    }
  }

  public class TypeInfo {
    /// Ci Associated Type
    public CiType Type;
    // Target language type name
    public string NewType;
    // Type must be declared?
    public bool IsNative;
    // Code to define the type (if Is Native is false)
    public string Definition;
    // Null value
    public string Null;
    // Null initialization
    public string NullInit;
    // Initialization code
    public string Init;
    // Array item type
    public string ItemType;
    // Array item default value
    public string ItemDefault;

    public TypeInfo() {
      this.IsNative = true;
    }

    public TypeInfo(CiType aCiType) : this(aCiType, aCiType.Name, "null") {
    }

    public TypeInfo(CiType aCiType, string aNewType) : this(aCiType, aNewType, "null") {
    }

    public TypeInfo(CiType aCiType, string aNewType, string aNull) {
      this.IsNative = true;
      this.Type = aCiType;
      this.NewType = aNewType;
      this.Null = aNull;
    }
  }

  public class UnaryOperatorMetadata {
    public Dictionary<CiToken, UnaryOperatorInfo> Metadata = new Dictionary<CiToken, UnaryOperatorInfo>();

    public UnaryOperatorMetadata() {
    }

    public void Declare(CiToken token, CiPriority priority, WriteUnaryOperatorDelegate writeDelegate, string prefix, string suffix) {
      UnaryOperatorInfo info = new UnaryOperatorInfo(token, priority, writeDelegate, prefix, suffix);
      if (!Metadata.ContainsKey(token)) {
        Metadata.Add(token, info);
      }
      else {
        Metadata[token] = info;
      }
    }

    public UnaryOperatorInfo GetUnaryOperator(CiToken token) {
      UnaryOperatorInfo result = null;
      Metadata.TryGetValue(token, out result);
      if (result == null) {
        throw new InvalidOperationException("No metadata for " + token);
      }
      return result;
    }
  }

  public class BinaryOperatorMetadata {
    public Dictionary<CiToken, BinaryOperatorInfo> Metadata = new Dictionary<CiToken, BinaryOperatorInfo>();

    public BinaryOperatorMetadata() {
    }

    public void Declare(CiToken token, CiPriority priority, bool associative, WriteBinaryOperatorDelegate writeDelegate, string symbol) {
      BinaryOperatorInfo info = new BinaryOperatorInfo(token, priority, associative, writeDelegate, symbol);
      if (!Metadata.ContainsKey(token)) {
        Metadata.Add(token, info);
      }
      else {
        Metadata[token] = info;
      }
    }

    public BinaryOperatorInfo GetBinaryOperator(CiToken token) {
      BinaryOperatorInfo result = null;
      Metadata.TryGetValue(token, out result);
      if (result == null) {
        throw new InvalidOperationException("No metadata for " + token);
      }
      return result;
    }
  }

  public class MappingMetadata<TYPE, DATA, INFO, RTYPE> where TYPE: class where DATA: class {
    //TODO merge the two delegate
    public delegate void ProcDelegate(DATA context);

    public delegate RTYPE FuncDelegate(DATA context);

    public class MappingData {
      public INFO Info;
      public ProcDelegate delegatedProc;
      public FuncDelegate delegatedFunc;
    }

    public Dictionary<TYPE, MappingData> Metadata = new Dictionary<TYPE, MappingData>();

    public MappingMetadata() {
    }

    public void Declare(TYPE typ, ProcDelegate delegat) {
      Declare(typ, default(INFO), delegat, null);
    }

    public void Declare(TYPE typ, FuncDelegate delegat) {
      Declare(typ, default(INFO), null, delegat);
    }

    public void Declare(TYPE typ, INFO info, ProcDelegate proc) {
      Declare(typ, info, proc, null);
    }

    public void Declare(TYPE typ, INFO info, FuncDelegate func) {
      Declare(typ, info, null, func);
    }

    public void Declare(TYPE typ, INFO info, ProcDelegate proc, FuncDelegate func) {
      if (!Metadata.ContainsKey(typ)) { 
        MappingData map = new MappingData();
        map.Info = info;
        map.delegatedProc = proc;
        map.delegatedFunc = func;
        Metadata.Add(typ, map);
      }
      else {
        MappingData map = Metadata[typ];
        map.Info = info;
        map.delegatedProc = proc;
        map.delegatedFunc = func;
      }
    }

    public bool ExcuteCall(TYPE typ, DATA context) {
      MappingData info = null;
      Metadata.TryGetValue(typ, out info);
      if (info != null) {
        info.delegatedProc(context);
      }
      return (info != null);
    }

    public bool ExcuteFunc(TYPE typ, DATA context, out RTYPE result) {
      MappingData info = null;
      Metadata.TryGetValue(typ, out info);
      if (info != null) {
        result = default(RTYPE);
        result = info.delegatedFunc(context);
      }
      else {
        result = default(RTYPE);
      }
      return (info != null);
    }

    public INFO GetInfo(TYPE typ) {
      MappingData info = null;
      Metadata.TryGetValue(typ, out info);
      return info.Info;
    }

    public ProcDelegate FindProcedure(string name, Object implementer) {
      ProcDelegate del = null;
      try {
        del = (ProcDelegate)Delegate.CreateDelegate(typeof(ProcDelegate), implementer, name);
      }
      catch (ArgumentNullException) {
      }
      catch (ArgumentException) {
      }
      catch (MissingMethodException) {
      }
      catch (MethodAccessException) {
      }
      return del;
    }

    public FuncDelegate FindFunction(string name, Object implementer) {
      FuncDelegate del = null;
      try {
        del = (FuncDelegate)Delegate.CreateDelegate(typeof(FuncDelegate), implementer, name);
      }
      catch (ArgumentNullException) {
      }
      catch (ArgumentException) {
      }
      catch (MissingMethodException) {
      }
      catch (MethodAccessException) {
      }
      if (del == null) {
        Console.WriteLine("Missing definition of " + name);
      }
      return del;
    }
  }

  public class GenericMetadata<TYPE> : MappingMetadata<Type, TYPE, CiPriority, object> where TYPE: class {
    public MappingData GetMetadata(TYPE obj) {
      Type type = obj.GetType();
      Type baseType = type;
      bool searched = false;
      MappingData info = null;
      while (info == null) {
        Metadata.TryGetValue(type, out info);
        if (info == null) {
          type = type.BaseType;
          searched = true;
          if (type == null) {
            throw new InvalidOperationException("No metadata for " + obj);
          }
        }
      }
      if (searched) {
        Metadata.Add(baseType, info);
      }
      return info;
    }

    public CiPriority GetPriority(TYPE obj) {
      MappingData info = GetMetadata(obj);
      return info.Info;
    }

    public void Translate(TYPE obj) {
      MappingData info = GetMetadata(obj);
      info.delegatedProc(obj);
    }
  }

  public class SymbolMapping {
    public DelegateGenerator Generator;
    public CiSymbol Symbol = null;
    public string NewName = "?";
    public SymbolMapping Parent = null;
    public List<SymbolMapping> childs = new List<SymbolMapping>();

    public SymbolMapping() {
    }

    public SymbolMapping(SymbolMapping aParent, CiSymbol aSymbol, string aNewName, DelegateGenerator Generator) {
      this.Generator = Generator;
      this.Symbol = aSymbol;
      this.Parent = aParent;
      if (aParent != null) {
        aParent.childs.Add(this);
      }
      this.NewName = aNewName;
    }

    public SymbolMapping(SymbolMapping aParent, CiSymbol aSymbol, bool inParentCheck, DelegateGenerator Generator) {
      this.Generator = Generator;
      this.Symbol = aSymbol;
      this.Parent = aParent;
      if (aParent != null) {
        aParent.childs.Add(this);
      }
      this.NewName = this.GetUniqueName(inParentCheck);
    }

    public bool IsUnique(string baseName, bool inParentCheck) {
      if (Generator.IsReservedWord(baseName)) {
        return false;
      }
      SymbolMapping context = this.Parent;
      while (context != null) {
        foreach (SymbolMapping item in context.childs) {
          if (String.Compare(item.NewName, baseName, true) == 0) {
            return false;
          }
        }
        if (inParentCheck) {
          context = context.Parent;
        }
        else {
          context = null;
        }
      }
      return true;
    }

    public string GetUniqueName(bool inParentCheck) {
      if (Symbol == null) {
        return "?";
      }
      string curName = Generator.TranslateSymbolName(Symbol);
      if (!IsUnique(curName, inParentCheck)) {
        string baseName = curName;
        curName = ((baseName.StartsWith("a") ? "an" : "a")) + char.ToUpperInvariant(baseName[0]) + baseName.Substring(1);
        if (!IsUnique(curName, inParentCheck)) {
          int suffix = 1;
          do {
            curName = baseName + "__" + suffix;
            suffix++;
          }
          while (!IsUnique(curName, inParentCheck));
        }
      }
      return curName;
    }
  }

  public abstract class DelegateGenerator : BaseGenerator {
    public DelegateGenerator(string aNamespace) : this() {
      SetNamespace(aNamespace);
    }

    public DelegateGenerator() : base() {
      TranslateSymbolName = SymbolNameTranslator;
      InitCiLanguage();
      InitOperators();
      InitLibrary();
    }

    public override void WriteProgram(CiProgram prog) {
      base.WriteProgram(prog);
      ClearUsedFunction();
      PreProcess(prog);
      EmitProgram(prog);
    }

    public abstract void EmitProgram(CiProgram prog);

    #region Ci Language Translation
    protected MappingMetadata<Type, CiType, CiPriority, TypeInfo> Types = new MappingMetadata<Type, CiType, CiPriority, TypeInfo>();
    protected GenericMetadata<CiSymbol> Symbols = new GenericMetadata<CiSymbol>();
    protected GenericMetadata<ICiStatement> Statemets = new GenericMetadata<ICiStatement>();
    protected GenericMetadata<CiExpr> Expressions = new GenericMetadata<CiExpr>();

    public virtual void InitCiLanguage() {
      //
      SetTypeTranslator(typeof(CiBoolType));
      SetTypeTranslator(typeof(CiByteType));
      SetTypeTranslator(typeof(CiIntType));
      SetTypeTranslator(typeof(CiStringPtrType));
      SetTypeTranslator(typeof(CiStringStorageType));
      SetTypeTranslator(typeof(CiClassPtrType));
      SetTypeTranslator(typeof(CiClassStorageType));
      SetTypeTranslator(typeof(CiArrayPtrType));
      SetTypeTranslator(typeof(CiArrayStorageType));
      SetTypeTranslator(typeof(CiEnum));
      //
      SetSymbolTranslator(typeof(CiEnum));
      SetSymbolTranslator(typeof(CiConst));
      SetSymbolTranslator(typeof(CiField));
      SetSymbolTranslator(typeof(CiMacro));
      SetSymbolTranslator(typeof(CiMethod));
      SetSymbolTranslator(typeof(CiClass));
      SetSymbolTranslator(typeof(CiDelegate));
      //
      SetStatementTranslator(typeof(CiBlock));
      SetStatementTranslator(typeof(CiConst));
      SetStatementTranslator(typeof(CiVar));
      SetStatementTranslator(typeof(CiExpr));
      SetStatementTranslator(typeof(CiAssign));
      SetStatementTranslator(typeof(CiDelete));
      SetStatementTranslator(typeof(CiBreak));
      SetStatementTranslator(typeof(CiContinue));
      SetStatementTranslator(typeof(CiDoWhile));
      SetStatementTranslator(typeof(CiFor));
      SetStatementTranslator(typeof(CiIf));
      SetStatementTranslator(typeof(CiNativeBlock));
      SetStatementTranslator(typeof(CiReturn));
      SetStatementTranslator(typeof(CiSwitch));
      SetStatementTranslator(typeof(CiThrow));
      SetStatementTranslator(typeof(CiWhile));
      //TODO Use reflection to define the expression priority
      Expressions.Declare(typeof(CiConstExpr), CiPriority.Postfix, Expressions.FindProcedure("Expression_CiConstExpr", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiConstAccess), CiPriority.Postfix, Expressions.FindProcedure("Expression_CiConstAccess", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiVarAccess), CiPriority.Postfix, Expressions.FindProcedure("Expression_CiVarAccess", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiFieldAccess), CiPriority.Postfix, Expressions.FindProcedure("Expression_CiFieldAccess", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiPropertyAccess), CiPriority.Postfix, Expressions.FindProcedure("Expression_CiPropertyAccess", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiArrayAccess), CiPriority.Postfix, Expressions.FindProcedure("Expression_CiArrayAccess", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiMethodCall), CiPriority.Postfix, Expressions.FindProcedure("Expression_CiMethodCall", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiBinaryResourceExpr), CiPriority.Postfix, Expressions.FindProcedure("Expression_CiBinaryResourceExpr", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiNewExpr), CiPriority.Postfix, Expressions.FindProcedure("Expression_CiNewExpr", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiUnaryExpr), CiPriority.Prefix, Expressions.FindProcedure("Expression_CiUnaryExpr", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiCondNotExpr), CiPriority.Prefix, Expressions.FindProcedure("Expression_CiCondNotExpr", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiPostfixExpr), CiPriority.Prefix, Expressions.FindProcedure("Expression_CiPostfixExpr", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiCondExpr), CiPriority.CondExpr, Expressions.FindProcedure("Expression_CiCondExpr", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiBinaryExpr), CiPriority.Lowest, Expressions.FindProcedure("Expression_CiBinaryExpr", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiCoercion), CiPriority.Lowest, Expressions.FindProcedure("Expression_CiCoercion", this) ?? IgnoreExpr);
    }

    public TypeInfo IgnoreType(CiType type) {
      return null;
    }

    public void SetTypeTranslator(Type type) {
      string name = "Type_" + type.Name;
      SetTypeTranslator(type, Types.FindFunction(name, this) ?? IgnoreType);
    }

    public void SetTypeTranslator(Type symbol, MappingMetadata<Type, CiType, CiPriority, TypeInfo>.FuncDelegate delegat) {
      Types.Declare(symbol, delegat);
    }

    public TypeInfo Translate(CiType type) {
      TypeInfo result = null;
      Types.ExcuteFunc(type.GetType(), type, out result);
      return result;
    }

    public void IgnoreSymbol(CiSymbol symbol) {
    }

    public void SetSymbolTranslator(Type symbol) {
      string name = "Symbol_" + symbol.Name;
      SetSymbolTranslator(symbol, Symbols.FindProcedure(name, this) ?? IgnoreSymbol);
    }

    public void SetSymbolTranslator(Type symbol, GenericMetadata<CiSymbol>.ProcDelegate delegat) {
      Symbols.Declare(symbol, delegat);
    }

    public void Translate(CiSymbol expr) {
      Symbols.Translate(expr);
    }

    public void IgnoreStatement(ICiStatement statement) {
    }

    public void SetStatementTranslator(Type statemenent) {
      string name = "Statement_" + statemenent.Name;
      SetStatementTranslator(statemenent, Statemets.FindProcedure(name, this) ?? IgnoreStatement);
    }

    public void SetStatementTranslator(Type statemenent, GenericMetadata<ICiStatement>.ProcDelegate delegat) {
      Statemets.Declare(statemenent, delegat);
    }

    public void Translate(ICiStatement expr) {
      Statemets.Translate(expr);
    }

    public void IgnoreExpr(CiExpr expression) {
    }

    public virtual void Translate(CiExpr expr) {
      Expressions.Translate(expr);
    }

    protected BinaryOperatorMetadata BinaryOperators = new BinaryOperatorMetadata();
    protected UnaryOperatorMetadata UnaryOperators = new UnaryOperatorMetadata();

    public virtual void InitOperators() {
      //TODO Use reflection to fill the structure
    }

    public void ConvertOperator(CiBinaryExpr expr, BinaryOperatorInfo token) {
      // Work-around to have correct left and right type
      GetExprType(expr);
      CiPriority priority = GetPriority(expr);
      // Force parantesis to increase readability
      if (expr.Left is CiCondExpr) {
        priority = CiPriority.Highest;
      }
      else if (expr.Left is CiBinaryExpr) {
        BinaryOperatorInfo info = BinaryOperators.GetBinaryOperator(((CiBinaryExpr)expr.Left).Op);
        if ((!info.Associative) || (info.Token != token.Token)) {
          priority = CiPriority.Highest;
        }
      }
      if (expr.Right is CiCondExpr) {
        priority = CiPriority.Highest;
      }
      else if (expr.Right is CiBinaryExpr) {
        BinaryOperatorInfo info = BinaryOperators.GetBinaryOperator(((CiBinaryExpr)expr.Right).Op);
        if ((!info.Associative) || (info.Token != token.Token)) {
          priority = CiPriority.Highest;
        }
      }
      WriteChild(priority, expr.Left, false);
      Write(token.Symbol);
      WriteChild(priority, expr.Right, !token.Associative);
    }

    public void ConvertOperatorUnary(CiUnaryExpr expr, UnaryOperatorInfo token) {
      Write(token.Prefix);
      WriteChild(expr, expr.Inner);
      Write(token.Suffix);
    }
    #endregion

    #region Library Translation
    protected MappingMetadata<CiProperty, CiPropertyAccess, CiPriority, object> Properties = new MappingMetadata<CiProperty, CiPropertyAccess, CiPriority,object>();
    protected MappingMetadata<CiMethod, CiMethodCall, CiPriority, object> Methods = new MappingMetadata<CiMethod, CiMethodCall, CiPriority,object>();

    public virtual void InitLibrary() {
      // Properties
      SetPropertyTranslator(CiLibrary.SByteProperty);
      SetPropertyTranslator(CiLibrary.LowByteProperty);
      SetPropertyTranslator(CiLibrary.StringLengthProperty);
      // Methods
      SetMethodTranslator(CiLibrary.MulDivMethod);
      SetMethodTranslator(CiLibrary.CharAtMethod);
      SetMethodTranslator(CiLibrary.SubstringMethod);
      SetMethodTranslator(CiLibrary.ArrayCopyToMethod);
      SetMethodTranslator(CiLibrary.ArrayToStringMethod);
      SetMethodTranslator(CiLibrary.ArrayStorageClearMethod);
    }

    public void SetPropertyTranslator(CiProperty prop) {
      string name = "Library_" + prop.Name;
      SetPropertyTranslator(prop, Properties.FindProcedure(name, this));
    }

    public void SetPropertyTranslator(CiProperty prop, MappingMetadata<CiProperty, CiPropertyAccess, CiPriority,object>.ProcDelegate del) {
      if (del == null) {
        throw new ArgumentNullException();
      }
      Properties.Declare(prop, del);
    }

    public void SetMethodTranslator(CiMethod met) {
      string name = "Library_" + met.Name;
      SetMethodTranslator(met, Methods.FindProcedure(name, this));
    }

    public void SetMethodTranslator(CiMethod met, MappingMetadata<CiMethod, CiMethodCall, CiPriority,object>.ProcDelegate del) {
      if (del == null) {
        throw new ArgumentNullException();
      }
      Methods.Declare(met, del);
    }

    public bool Translate(CiPropertyAccess prop) {
      return (prop.Property != null) ? Properties.ExcuteCall(prop.Property, prop) : false;
    }

    public bool Translate(CiMethodCall call) {
      return (call.Method != null) ? Methods.ExcuteCall(call.Method, call) : false;
    }
    #endregion

    #region Pre Processor
    // If true local method variable are processed in order to obtain unique name in the context
    protected bool ExpandVar = false;
    // If true local method parameter are processed in order to obtain unique name in the context
    protected bool CheckParam = false;

    public virtual bool Execute(ICiStatement[] stmt, StatementActionDelegate action) {
      if (stmt != null) {
        foreach (ICiStatement s in stmt) {
          if (Execute(s, action)) {
            return true;
          }
        }
      }
      return false;
    }

    public virtual bool Execute(ICiStatement stmt, StatementActionDelegate action) {
      if (stmt == null) {
        return false;
      }
      if (action(stmt)) {
        return true;
      }
      if (stmt is CiBlock) {
        if (Execute(((CiBlock)stmt).Statements, action)) {
          return true;
        }
      }
      else if (stmt is CiFor) {
        CiFor loop = (CiFor)stmt;
        if (Execute(loop.Init, action)) {
          return true;
        }
        if (Execute(loop.Body, action)) {
          return true;
        }
        if (Execute(loop.Advance, action)) {
          return true;
        }
      }
      else if (stmt is CiLoop) {
        CiLoop loop = (CiLoop)stmt;
        if (Execute(loop.Body, action)) {
          return true;
        }
      }
      else if (stmt is CiIf) {
        CiIf iiff = (CiIf)stmt;
        if (Execute(iiff.OnTrue, action)) {
          return true;
        }
        if (Execute(iiff.OnFalse, action)) {
          return true;
        }
      }
      else if (stmt is CiSwitch) {
        CiSwitch swith = (CiSwitch)stmt;
        foreach (CiCase cas in swith.Cases) {
          if (Execute(cas.Body, action)) {
            return true;
          }
        }
        if (Execute(swith.DefaultBody, action)) {
          return true;
        }
      }
      return false;
    }

    protected bool promoteClassConst = true;

    public virtual void PreProcess(CiProgram program) {
      ResetSymbolMapping();
      ResetClassOrder();
      ResetType();
      ResetContext();
      ResetMethodStack();
      ResetExprType();
      SymbolMapping root = new SymbolMapping();
      foreach (CiSymbol symbol in program.Globals) {
        if (!(symbol is CiClass)) {
          SymbolMapping parent = AddSymbol(root, symbol);
          if (symbol is CiEnum) {
            foreach (CiEnumValue val in ((CiEnum)symbol).Values) {
              AddSymbol(parent, val);
            }
          }
        }
      }
      foreach (CiSymbol symbol in program.Globals) {
        if (symbol is CiClass) {
          AddClass((CiClass)symbol);
        }
      }
      foreach (CiClass klass in GetOrderedClassList()) {
        SymbolMapping parent = (klass.BaseClass != null ? FindSymbol(klass.BaseClass) : root);
        AddSymbol(parent, klass);
        if (promoteClassConst) {
          foreach (CiConst konst in klass.ConstArrays) {
            AddType(((CiConst)konst).Type);
            AddSymbol(root, konst);
          }
        }
      }
      foreach (CiClass klass in GetOrderedClassList()) {
        PreProcess(program, klass);
      }
    }

    public virtual void PreProcess(CiProgram program, CiClass klass) {
      SymbolMapping parent = FindSymbol(klass);
      foreach (CiSymbol member in klass.Members) {
        if (member is CiConst) {
          if (!promoteClassConst) {
            AddType(((CiConst)member).Type);
            AddSymbol(parent, member);
          }
        }
        else if (!(member is CiMethod)) {
          AddSymbol(parent, member);
        }
        if (member is CiField) {
          AddType(((CiField)member).Type);
        }
      }
      foreach (CiBinaryResource resource in klass.BinaryResources) {
        AddSymbol(parent, resource);
        AddType(resource.Type);
      }
      if (klass.Constructor != null) {
        AddSymbol(parent, klass.Constructor);
      }
      foreach (CiSymbol member in klass.Members) {
        if (member is CiMethod) {
          SymbolMapping methodContext = AddSymbol(parent, member, false);
          CiMethod method = (CiMethod)member;
          AddSymbol(parent, method.Signature, methodContext.NewName);
          if (method.Signature.Params.Length > 0) {
            SymbolMapping methodCall = AddSymbol(methodContext, null);
            foreach (CiParam p in method.Signature.Params) {
              AddSymbol(methodCall, p, CheckParam);
              AddType(p.Type);
            }
          }
          AddType(method.Signature.ReturnType);
        }
      }
      if (klass.Constructor != null) {
        PreProcess(klass, klass.Constructor);
      }
      foreach (CiSymbol member in klass.Members) {
        if (member is CiMethod) {
          PreProcess(klass, (CiMethod)member);
        }
      }
    }

    public virtual void PreProcess(CiClass klass, CiMethod method) {
      Execute(method.Body, s => PreProcess(method, s));
    }

    public virtual bool PreProcess(CiMethod method, ICiStatement stmt) {
      if (ExpandVar) {
        if (stmt is CiVar) {
          CiVar v = (CiVar)stmt;
          SymbolMapping parent = FindSymbol(method);
          AddSymbol(parent, v);
          AddType(v.Type);
        }
      }
      return false;
    }
    #endregion

    #region Symbol Mapper
    //
    public TranslateSymbolNameDelegate TranslateSymbolName {
      get;
      set;
    }

    protected HashSet<string> ReservedWords = null;
    protected Dictionary<CiSymbol, SymbolMapping> varMap = new  Dictionary<CiSymbol, SymbolMapping>();
    //
    public abstract string[] GetReservedWords();

    public void ResetSymbolMapping() {
      string[] words = GetReservedWords();
      if (words != null) {
        ReservedWords = new HashSet<string>(words);
      }
      else {
        ReservedWords = new HashSet<string>();
      }
      varMap.Clear();
    }

    public bool HasSymbols() {
      return varMap.Count == 0;
    }

    public bool IsReservedWord(string aName) {
      return ReservedWords.Contains(aName.ToLower());
    }

    public SymbolMapping AddSymbol(SymbolMapping aParent, CiSymbol aSymbol) {
      return AddSymbol(aParent, aSymbol, true);
    }

    public SymbolMapping AddSymbol(SymbolMapping aParent, CiSymbol aSymbol, bool inParentCheck) {
      SymbolMapping item = new SymbolMapping(aParent, aSymbol, inParentCheck, this);
      if (aSymbol != null) {
        try {
          varMap.Add(aSymbol, item);
        }
        catch (ArgumentException) {
          throw new ArgumentException("Symbol " + aSymbol.Name + " already added");
        }
      }
      return item;
    }

    public SymbolMapping AddSymbol(SymbolMapping aParent, CiSymbol aSymbol, string aNewName) {
      SymbolMapping item = new SymbolMapping(aParent, aSymbol, false, this);
      item.NewName = aNewName;
      if (aSymbol != null) {
        varMap.Add(aSymbol, item);
      }
      return item;
    }

    public SymbolMapping FindSymbol(CiSymbol symbol) {
      SymbolMapping result = null;
      varMap.TryGetValue(symbol, out result);
      return result;
    }

    public string SymbolNameTranslator(CiSymbol aSymbol) {
      String name = aSymbol.Name;
      StringBuilder tmpName = new StringBuilder(name.Length);
      foreach (char c in name) {
        tmpName.Append(CiLexer.IsLetter(c) ? c : '_');
      }
      string baseName = tmpName.ToString();
      if (IsReservedWord(baseName)) {
        baseName = ((baseName.StartsWith("a") ? "an" : "a")) + char.ToUpperInvariant(baseName[0]) + baseName.Substring(1);
      }
      return baseName;
    }
    #endregion

    #region Class Order
    protected List<CiClass> classOrder = new List<CiClass>();

    public void ResetClassOrder() {
      classOrder.Clear();
    }

    public List<CiClass>GetOrderedClassList() {
      return classOrder;
    }

    public void AddClass(CiClass klass) {
      if (klass == null) {
        return;
      }
      if (classOrder.Contains(klass)) {
        return;
      }
      AddClass(klass.BaseClass);
      classOrder.Add(klass);
    }
    #endregion

    #region Type Mapper
    protected HashSet<CiClass> refClass = new HashSet<CiClass>();
    protected HashSet<CiType> refType = new HashSet<CiType>();
    protected Dictionary<CiType, TypeInfo> TypeCache = new Dictionary<CiType, TypeInfo>();

    public HashSet<CiClass>GetClassTypeList() {
      return refClass;
    }

    public HashSet<CiType>GetTypeList() {
      return refType;
    }

    public virtual string DecodeType(CiType type) {
      string typeDec = GetTypeInfo(type).NewType;
      if (type is CiArrayStorageType) {
        //TODO handle LengthExpr
        return String.Format(typeDec, ((CiArrayStorageType)type).Length);
      }
      else {
        return typeDec;
      }
    }

    public TypeInfo GetTypeInfo(CiType type) {
      if (TypeCache.ContainsKey(type)) {
        return TypeCache[type];
      }
      TypeInfo info = null;
      if (Types.ExcuteFunc(type.GetType(), type, out info)) {
        TypeCache.Add(type, info);
      }
      else {
        info = new TypeInfo(type);
      }
      return info;
    }

    public void AddType(CiType type) {
      if (type == null) {
        return;
      }
      if (refType.Contains(type)) {
        return;
      }
      if (type is CiArrayType) {
        CiArrayType arr = (CiArrayType)type;
        if (arr.BaseType is CiClassType) {
          CiClass klass = ((CiClassType)arr.BaseType).Class;
          if (!refClass.Contains(klass)) {
            refClass.Add(klass);
          }
        }
      }
      if (type is CiClassType) {
        CiClass klass = ((CiClassType)type).Class;
        if (!refClass.Contains(klass)) {
          refClass.Add(klass);
        }
      }
      refType.Add(type);
    }

    public void ResetType() {
      refClass.Clear();
      refType.Clear();
      TypeCache.Clear();
    }
    #endregion

    #region Helper
    protected int ElemPerRow = 16;
    protected string ElemSeparator = ", ";

    public Int32 ExprAsInteger(CiExpr e, Int32 def) {
      int res = def;
      if (e is CiConstExpr) {
        object v = ((CiConstExpr)e).Value;
        if (v is Int32) {
          res = (Int32)v;
        }
        else if (v is byte) {
          res = (byte)v;
        }
      }
      return res;
    }

    public int GetArraySize(CiType type) {
      if (type is CiArrayStorageType) {
        CiArrayStorageType arr = (CiArrayStorageType)type;
        if (arr.LengthExpr == null) {
          return ((CiArrayStorageType)type).Length;
        }
      }
      return -1;
    }

    public virtual string DecodeArray(CiType type, Array array) {
      StringBuilder res = new StringBuilder();
      if (array.Length >= ElemPerRow) {
        res.Append(NewLineStr);
        OpenBlock(false);
        AppendIndentStr(res);
      }
      for (int i = 0; i < array.Length; i++) {
        res.Append(DecodeValue(type, array.GetValue(i)));
        if (i < (array.Length - 1)) {
          res.Append(ElemSeparator);
          if ((i + 1) % ElemPerRow == 0) {
            res.Append(NewLineStr);
            AppendIndentStr(res);
          }
        }
      }
      if (array.Length >= ElemPerRow) {
        CloseBlock(false);
        res.Append(NewLineStr);
        AppendIndentStr(res);
      }
      return res.ToString();
    }

    protected string Decode_ARRAYBEGIN = "{ ";
    protected string Decode_ARRAYEND = " }";
    protected string Decode_TRUEVALUE = "true";
    protected string Decode_FALSEVALUE = "false";
    protected string Decode_ENUMFORMAT = "{0}.{1}";
    protected string Decode_NULLVALUE = "null";
    protected string Decode_STRINGBEGIN = "\"";
    protected string Decode_STRINGEND = "\"";
    protected string Decode_NONANSICHAR = "{0}";
    protected Dictionary<char, string> Decode_SPECIALCHAR = new Dictionary<char, string>();

    public virtual string DecodeValue(CiType type, object value) {
      StringBuilder res = new StringBuilder();
      if (value is bool) {
        res.Append((bool)value ? Decode_TRUEVALUE : Decode_FALSEVALUE);
      }
      else if (value is byte) {
        res.Append((byte)value);
      }
      else if (value is int) {
        res.Append((int)value);
      }
      else if (value is string) {
        res.Append(Decode_STRINGBEGIN);
        foreach (char c in (string) value) {
          if (Decode_SPECIALCHAR.ContainsKey(c)) {
            res.Append(Decode_SPECIALCHAR[c]);
          }
          else if (((int)c < 32) || ((int)c > 126)) {
            res.AppendFormat(Decode_NONANSICHAR, c, (int)c);
          }
          else {
            res.Append(c);
          }
        }
        res.Append(Decode_STRINGEND);
      }
      else if (value is CiEnumValue) {
        CiEnumValue ev = (CiEnumValue)value;
        res.AppendFormat(Decode_ENUMFORMAT, DecodeSymbol(ev.Type), DecodeSymbol(ev));
      }
      else if (value is Array) {
        res.Append(Decode_ARRAYBEGIN);
        res.Append(DecodeArray(type, (Array)value));
        res.Append(Decode_ARRAYEND);
      }
      else if (value == null) {
        TypeInfo info = GetTypeInfo(type);
        res.Append(info.Null ?? Decode_NULLVALUE);
      }
      else {
        throw new ArgumentException(value.ToString());
      }
      return res.ToString();
    }

    public virtual string DecodeSymbol(CiSymbol symbol) {
      SymbolMapping mappedSymbol = FindSymbol(symbol);
      return (mappedSymbol != null) ? mappedSymbol.NewName : TranslateSymbolName(symbol);
    }

    public virtual void WriteChild(CiExpr parent, CiExpr child) {
      WriteChild(GetPriority(parent), child, false);
    }

    public virtual void WriteChild(CiExpr parent, CiExpr child, bool nonAssoc) {
      WriteChild(GetPriority(parent), child, nonAssoc);
    }

    public virtual void WriteChild(CiPriority parentPriority, CiExpr child) {
      WriteChild(parentPriority, child, false);
    }

    public virtual void WriteChild(CiPriority parentPriority, CiExpr child, bool nonAssoc) {
      GenericMetadata<CiExpr>.MappingData exprInfo = Expressions.GetMetadata(child);
      CiPriority priority = GetPriority(child);
      bool par = false;
      if ((priority < parentPriority) || (nonAssoc && (priority == parentPriority))) {
        if ((child is CiUnaryExpr) || (child is CiBinaryExpr) || (child is CiCondExpr)) {
          par = true;
        }
      }
      if (par) {
        Write('(');
      }
      exprInfo.delegatedProc(child);
      if (par) {
        Write(')');
      }
    }

    public virtual CiPriority GetPriority(CiExpr expr) {
      CiPriority result;
      if (expr is CiCoercion) {
        result = GetPriority((CiExpr)((CiCoercion)expr).Inner);
      }
      else if (expr is CiBinaryExpr) {
        result = BinaryOperators.GetBinaryOperator(((CiBinaryExpr)expr).Op).Priority;
      }
      else {
        result = Expressions.GetPriority(expr);
      }
      return result;
    }

    private HashSet<string> UsedFunc = new HashSet<string>();

    public void ClearUsedFunction() {
      UsedFunc.Clear();
    }

    public void UseFunction(string name) {
      if (!UsedFunc.Contains(name)) {
        UsedFunc.Add(name);
      }
    }

    public bool IsUsedFunction(string name) {
      return UsedFunc.Contains(name);
    }

    public bool HasUsedFunction() {
      return UsedFunc.Count > 0;
    }
    #endregion

    #region Context Tracker
    private Stack<int> instructions = new Stack<int>();

    public void ResetContext() {
      instructions.Clear();
    }

    public void EnterContext(int step) {
      instructions.Push(step);
    }

    public void ExitContext() {
      instructions.Pop();
    }

    public bool InContext(int inst) {
      return instructions.Any(step => (step == inst));
    }
    #endregion

    #region MethodStack
    private Stack<CiMethod> methods = new Stack<CiMethod>();

    public void ResetMethodStack() {
      methods.Clear();
    }

    public void EnterMethod(CiMethod call) {
      methods.Push(call);
    }

    public void ExitMethod() {
      methods.Pop();
    }

    public CiMethod CurrentMethod() {
      if (methods.Count > 0) {
        return methods.Peek();
      }
      return null;
    }
    #endregion

    #region ExprType
    private Dictionary<CiExpr, CiType> exprMap = new  Dictionary<CiExpr, CiType>();

    public void ResetExprType() {
      exprMap.Clear();
    }

    public CiType GetExprType(CiExpr expr) {
      CiType result;
      exprMap.TryGetValue(expr, out result);
      if (result == null) {
        result = AnalyzeExpr(expr);
        exprMap.Add(expr, result);
      }
      return result;
    }

    public CiType AnalyzeExpr(CiExpr expr) {
      if (expr == null) {
        return CiType.Null;
      }
      else if (expr is CiUnaryExpr) {
        var e = (CiUnaryExpr)expr;
        CiType t = GetExprType(e.Inner);
        return t;
      }
      else if (expr is CiPostfixExpr) {
        var e = (CiPostfixExpr)expr;
        CiType t = GetExprType(e.Inner);
        return t;
      }
      else if (expr is CiBinaryExpr) {
        var e = (CiBinaryExpr)expr;
        CiType left = GetExprType(e.Left);
        CiType right = GetExprType(e.Right);
        CiType t = ((left == null) || (left == CiType.Null)) ? right : left;
        exprMap[e.Left] = t;
        exprMap[e.Right] = t;
        return t;
      }
      else {
        return expr.Type;
      }
    }
    #endregion

    #region JavaDoc
    protected string CommentContinueStr = "/// ";
    protected string CommentBeginStr = "";
    protected string CommentEndStr = "";
    protected string CommentCodeBegin = "`";
    protected string CommentCodeEnd = "`";
    protected string CommentListBegin = "";
    protected string CommentListEnd = "";
    protected string CommentItemListBegin = "* ";
    protected string CommentItemListEnd = "";
    protected string CommentSummaryBegin = "";
    protected string CommentSummaryEnd = "";
    protected string CommentRemarkBegin = "";
    protected string CommentRemarkEnd = "";
    protected Dictionary<string, string> CommentSpecialCode = new Dictionary<string, string>();
    protected Dictionary<char, string> CommentSpecialChar = new Dictionary<char, string>();

    protected virtual void WriteDocString(string text) {
      foreach (char c in text) {
        if (CommentSpecialChar.ContainsKey(c)) {
          Write(CommentSpecialChar[c]);
        }
        else if (c == '\n') {
          WriteLine();
          Write(CommentContinueStr);
        }
        else {
          Write(c);
        }
      }
    }

    protected virtual void WriteDocPara(CiDocPara para) {
      foreach (CiDocInline inline in para.Children) {
        CiDocText text = inline as CiDocText;
        if (text != null) {
          WriteDocString(text.Text);
          continue;
        }
        CiDocCode code = inline as CiDocCode;
        if (code != null) {
          Write(CommentCodeBegin);
          string codeText = code.Text ?? "";
          if (CommentSpecialCode.ContainsKey(codeText)) {
            Write(CommentSpecialCode[codeText]);
          }
          else {
            WriteDocString(codeText);
          }

          Write(CommentCodeEnd);
          continue;
        }
        throw new ArgumentException(inline.GetType().Name);
      }
    }

    protected virtual void WriteDocBlock(CiDocBlock block) {
      CiDocList list = block as CiDocList;
      if (list != null) {
        WriteLine(CommentListBegin);
        foreach (CiDocPara item in list.Items) {
          Write(CommentContinueStr);
          Write(CommentItemListBegin);
          WriteDocPara(item);
          WriteLine(CommentItemListEnd);
        }
        Write(CommentContinueStr);
        Write(CommentListEnd);
      }
      else {
        WriteDocPara((CiDocPara)block);
      }
    }

    protected void WriteDontClose(CiCodeDoc doc) {
      if (!String.IsNullOrEmpty(CommentBeginStr)) {
        WriteLine(CommentBeginStr);
      }
      Write(CommentContinueStr);
      if (doc.Summary.Children.Length > 0) {
        Write(CommentSummaryBegin);
        WriteDocPara(doc.Summary);
        WriteLine(CommentSummaryEnd);
      }
      if (doc.Details.Length > 0) {
        if (!String.IsNullOrEmpty(CommentRemarkBegin)) {
          Write(CommentContinueStr);
          WriteLine(CommentRemarkBegin);
        }
        foreach (CiDocBlock block in doc.Details) {
          Write(CommentContinueStr);
          WriteDocBlock(block);
          WriteLine();
        }
        if (!String.IsNullOrEmpty(CommentRemarkEnd)) {
          Write(CommentContinueStr);
          WriteLine(CommentRemarkEnd);
        }
      }
    }

    protected virtual void WriteDocCode(CiCodeDoc doc) {
      if (doc != null) {
        WriteDontClose(doc);
        if (!String.IsNullOrEmpty(CommentEndStr)) {
          WriteLine(CommentEndStr);
        }
      }
    }

    protected virtual void WriteDocMethod(CiMethod method) {
      if (method.Documentation != null) {
        WriteDontClose(method.Documentation);
        foreach (CiParam param in method.Signature.Params) {
          if (param.Documentation != null) {
            Write(CommentContinueStr);
            Write("@param ");
            Write(DecodeSymbol(param));
            Write(' ');
            WriteDocPara(param.Documentation.Summary);
            WriteLine();
          }
        }
        if (!String.IsNullOrEmpty(CommentEndStr)) {
          WriteLine(CommentEndStr);
        }
      }
    }
    #endregion JavaDoc
  }
}
