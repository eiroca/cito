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
  public delegate void WriteUnaryOperatorDelegate(CiUnaryExpr expr,UnaryOperatorInfo token);
  public delegate void WriteBinaryOperatorDelegate(CiBinaryExpr expr,BinaryOperatorInfo token);
  //
  public delegate void WritePropertyAccessDelegate(CiPropertyAccess expr);
  public delegate void WriteMethodDelegate(CiMethodCall method);
  //
  public delegate string TranslateSymbolNameDelegate(CiSymbol aSymbol);
  //
  public delegate TypeInfo TranslateTypeDelegate(CiType type);
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
    public bool ForcePar;
    public string Symbol;
    public WriteBinaryOperatorDelegate WriteDelegate;

    public BinaryOperatorInfo(CiToken token, CiPriority priority, WriteBinaryOperatorDelegate writeDelegate, string symbol) {
      this.ForcePar = true;
      if ((token == CiToken.Plus) || (token == CiToken.Minus) || (token == CiToken.Asterisk) || (token == CiToken.Slash)) {
        this.ForcePar = false;
      }
      this.Token = token;
      this.Priority = priority;
      this.Symbol = symbol;
      this.WriteDelegate = writeDelegate;
    }
  }

  public class ExpressionInfo {
    public CiPriority Priority;
    public WriteExprDelegate WriteDelegate;

    public ExpressionInfo(CiPriority priority, WriteExprDelegate writeDelegate) {
      this.Priority = priority;
      this.WriteDelegate = writeDelegate;
    }
  }

  public class TypeInfo {
    public CiType Type;
    public string Name;
    public string Definition;
    public bool IsNative;
    public string Null;
    public string NullInit;
    public string Init;
    public int Level;
    public string ItemType;
    public string ItemDefault;
  }

  public class UnaryOperatorMetadata {
    public Dictionary<CiToken, UnaryOperatorInfo> Metadata = new Dictionary<CiToken, UnaryOperatorInfo>();

    public UnaryOperatorMetadata() {
    }

    public void Add(CiToken token, CiPriority priority, WriteUnaryOperatorDelegate writeDelegate, string prefix, string suffix) {
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
        throw new InvalidOperationException("No delegate for " + token);
      }
      return result;
    }
  }

  public class BinaryOperatorMetadata {
    public Dictionary<CiToken, BinaryOperatorInfo> Metadata = new Dictionary<CiToken, BinaryOperatorInfo>();

    public BinaryOperatorMetadata() {
    }

    public void Add(CiToken token, CiPriority priority, WriteBinaryOperatorDelegate writeDelegate, string symbol) {
      BinaryOperatorInfo info = new BinaryOperatorInfo(token, priority, writeDelegate, symbol);
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
        throw new InvalidOperationException("No delegate for " + token);
      }
      return result;
    }
  }

  public class ExpressionMetadata {
    public Dictionary<Type, ExpressionInfo> Metadata = new Dictionary<Type, ExpressionInfo>();

    public ExpressionMetadata() {
    }

    public void Add(Type exprType, CiPriority priority, WriteExprDelegate writeDelegate) {
      ExpressionInfo info = new ExpressionInfo(priority, writeDelegate);
      if (!Metadata.ContainsKey(exprType)) {
        Metadata.Add(exprType, info);
      }
      else {
        Metadata[exprType] = info;
      }
    }

    public ExpressionInfo GetInfo(CiExpr expr) {
      ExpressionInfo result = null;
      Type exprType = expr.GetType();
      Type baseType = exprType;
      bool searched = false;
      while (exprType != null) {
        Metadata.TryGetValue(exprType, out result);
        if (result != null) {
          break;
        }
        exprType = exprType.BaseType;
        searched = true;
      }
      if (result == null) {
        throw new InvalidOperationException("No delegate for " + expr.GetType());
      }
      if (searched) {
        Metadata.Add(baseType, result);
      }
      return result;
    }

    public virtual void Translate(CiExpr expr) {
      ExpressionInfo exprInfo = GetInfo(expr);
      if (exprInfo != null) {
        exprInfo.WriteDelegate(expr);
      }
      else {
        throw new ArgumentException(expr.ToString());
      }
    }
  }

  public class DelegateMappingMetadata<TYPE, DATA> where TYPE: class where DATA: class {
    public delegate void Delegator(DATA context);

    public Dictionary<TYPE, Delegator> Metadata = new Dictionary<TYPE, Delegator>();

    public DelegateMappingMetadata() {
    }

    public void Add(TYPE typ, Delegator delegat) {
      if (!Metadata.ContainsKey(typ)) { 
        Metadata.Add(typ, delegat);
      }
      else {
        Metadata[typ] = delegat;
      }
    }

    public bool CallDelegate(TYPE typ, DATA context) {
      Delegator callDelegate = null;
      Metadata.TryGetValue(typ, out callDelegate);
      if (callDelegate != null) {
        callDelegate(context);
      }
      return (callDelegate != null);
    }

    public Delegator FindAppropriate(string name, Object implementer) {
      Delegator del = null;
      try {
        del = (Delegator)Delegate.CreateDelegate(typeof(Delegator), implementer, name);
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
  }

  public class GenericMetadata<TYPE> : DelegateMappingMetadata<Type, TYPE> where TYPE: class {
    public void Translate(TYPE obj) {
      Type type = obj.GetType();
      Type baseType = type;
      bool found = false;
      bool searched = false;
      Delegator del = null;
      while (!found) {
        Metadata.TryGetValue(type, out del);
        found = (del != null);
        if (found) {
          del(obj);
        }
        else {
          type = type.BaseType;
          searched = true;
          if (type == null) {
            throw new InvalidOperationException("No delegate for " + obj);
          }
        }
      }
      if (searched) {
        Metadata.Add(baseType, del);
      }
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
      InitExpressions();
      InitOperators();
      InitLibrary();
    }

    public override void Write(CiProgram prog) {
      PreProcess(prog);
      EmitProgram(prog);
    }

    public abstract void EmitProgram(CiProgram prog);

    #region Ci Language Translation
    public virtual void InitCiLanguage() {
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
    }

    protected GenericMetadata<CiSymbol> Symbols = new GenericMetadata<CiSymbol>();
    protected GenericMetadata<ICiStatement> Statemets = new GenericMetadata<ICiStatement>();

    public void SetSymbolTranslator(Type symbol) {
      string name = "Symbol_" + symbol.Name;
      SetSymbolTranslator(symbol, Symbols.FindAppropriate(name, this) ?? IgnoreSymbol);
    }

    public void SetSymbolTranslator(Type symbol, GenericMetadata<CiSymbol>.Delegator delegat) {
      Symbols.Add(symbol, delegat);
    }

    public void SetStatementTranslator(Type statemenent) {
      string name = "Statement_" + statemenent.Name;
      SetStatementTranslator(statemenent, Statemets.FindAppropriate(name, this) ?? IgnoreStatement);
    }

    public void SetStatementTranslator(Type statemenent, GenericMetadata<ICiStatement>.Delegator delegat) {
      Statemets.Add(statemenent, delegat);
    }

    public void Translate(CiSymbol expr) {
      Symbols.Translate(expr);
    }

    public void Translate(ICiStatement expr) {
      Statemets.Translate(expr);
    }

    public void IgnoreSymbol(CiSymbol symbol) {
    }

    public void IgnoreStatement(ICiStatement statement) {
    }

    protected ExpressionMetadata Expressions = new ExpressionMetadata();
    protected BinaryOperatorMetadata BinaryOperators = new BinaryOperatorMetadata();
    protected UnaryOperatorMetadata UnaryOperators = new UnaryOperatorMetadata();

    public virtual void InitExpressions() {
      //TODO Use reflection to fill the structure
    }

    public virtual void InitOperators() {
      //TODO Use reflection to fill the structure
    }

    public void Translate(CiExpr expr) {
      Expressions.Translate(expr);
    }
    #endregion

    #region Library Translation
    protected DelegateMappingMetadata<CiProperty, CiPropertyAccess> Properties = new DelegateMappingMetadata<CiProperty, CiPropertyAccess>();
    protected DelegateMappingMetadata<CiMethod, CiMethodCall> Methods = new DelegateMappingMetadata<CiMethod, CiMethodCall>();

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
      SetPropertyTranslator(prop, Properties.FindAppropriate(name, this));
    }

    public void SetPropertyTranslator(CiProperty prop, DelegateMappingMetadata<CiProperty, CiPropertyAccess>.Delegator del) {
      if (del == null) {
        throw new ArgumentNullException();
      }
      Properties.Add(prop, del);
    }

    public void SetMethodTranslator(CiMethod met) {
      string name = "Library_" + met.Name;
      SetMethodTranslator(met, Methods.FindAppropriate(name, this));
    }

    public void SetMethodTranslator(CiMethod met, DelegateMappingMetadata<CiMethod, CiMethodCall>.Delegator del) {
      if (del == null) {
        throw new ArgumentNullException();
      }
      Methods.Add(met, del);
    }

    public bool Translate(CiPropertyAccess prop) {
      return (prop.Property != null) ? Properties.CallDelegate(prop.Property, prop) : false;
    }

    public bool Translate(CiMethodCall call) {
      return (call.Method != null) ? Methods.CallDelegate(call.Method, call) : false;
    }
    #endregion

    #region Pre Processor
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

    public virtual void PreProcess(CiProgram program) {
      ResetSymbolMapping();
      ResetClassOrder();
      ResetType();
      ResetContext();
      ResetMethodStack();
      ResetExprType();
      SymbolMapping root = new SymbolMapping();
      foreach (CiSymbol symbol in program.Globals) {
        if (symbol is CiEnum) {
          AddSymbol(root, symbol);
        }
      }
      foreach (CiSymbol symbol in program.Globals) {
        if (symbol is CiDelegate) {
          AddSymbol(root, symbol);
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
      }
      foreach (CiClass klass in GetOrderedClassList()) {
        PreProcess(program, klass);
      }
    }

    public virtual void PreProcess(CiProgram program, CiClass klass) {
      SymbolMapping parent = FindSymbol(klass);
      foreach (CiSymbol member in klass.Members) {
        if (member is CiField) {
          AddSymbol(parent, member);
          AddType(((CiField)member).Type);
        }
      }
      foreach (CiConst konst in klass.ConstArrays) {
        AddSymbol(parent, konst);
        AddType(konst.Type);
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
          if (method.Signature.Params.Length > 0) {
            SymbolMapping methodCall = AddSymbol(methodContext, null);
            foreach (CiParam p in method.Signature.Params) {
              AddSymbol(methodCall, p);
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
      return false;
    }
    #endregion

    #region Symbol Mapper
    //
    public TranslateSymbolNameDelegate TranslateSymbolName { get; set; }

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
      if (IsReservedWord(name)) {
        name = "_" + name;
      }
      return name;
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
    public TranslateTypeDelegate TranslateType { get; set; }

    protected HashSet<CiClass> refClass = new HashSet<CiClass>();
    protected HashSet<CiType> refType = new HashSet<CiType>();
    protected Dictionary<CiType, TypeInfo> TypeCache = new Dictionary<CiType, TypeInfo>();

    public HashSet<CiClass>GetClassTypeList() {
      return refClass;
    }

    public HashSet<CiType>GetTypeList() {
      return refType;
    }

    public string GetTypeName(CiType type) {
      return GetTypeInfo(type).Name;
    }

    public TypeInfo GetTypeInfo(CiType type) {
      if (TypeCache.ContainsKey(type)) {
        return TypeCache[type];
      }
      TypeInfo info = TranslateType(type);
      TypeCache.Add(type, info);
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
      }
      for (int i = 0; i < array.Length; i++) {
        res.Append(DecodeValue(type, array.GetValue(i)));
        if (i < (array.Length - 1)) {
          res.Append(ElemSeparator);
          if (i % ElemPerRow == 0) {
            res.Append(NewLineStr);
          }
        }
      }
      if (array.Length >= ElemPerRow) {
        CloseBlock(false);
        res.Append(NewLineStr);
      }
      return res.ToString();
    }

    public virtual string DecodeValue(CiType type, object value) {
      return value.ToString();
    }

    public string DecodeSymbol(CiSymbol var) {
      SymbolMapping symbol = FindSymbol(var);
      return (symbol != null) ? symbol.NewName : var.Name;
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
      ExpressionInfo exprInfo = Expressions.GetInfo(child);
      if (exprInfo == null) {
        throw new ArgumentException(child.ToString());
      }
      if ((exprInfo.Priority < parentPriority) || (nonAssoc && (exprInfo.Priority == parentPriority))) {
        Write('(');
        exprInfo.WriteDelegate(child);
        Write(')');
      }
      else {
        exprInfo.WriteDelegate(child);
      }
    }

    public virtual CiPriority GetPriority(CiExpr expr) {
      if (expr is CiCoercion) {
        return GetPriority((CiExpr)((CiCoercion)expr).Inner);
      }
      if (expr is CiBinaryExpr) {
        return BinaryOperators.GetBinaryOperator(((CiBinaryExpr)expr).Op).Priority;
      }
      ExpressionInfo exprInfo = Expressions.GetInfo(expr);
      if (exprInfo != null) {
        return exprInfo.Priority;
      }
      throw new ArgumentException(expr.GetType().Name);
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
      if (expr == null)
        return CiType.Null;
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

  }
}