﻿using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GraphView.GremlinTranslationOps.branch
{
    internal class GremlinChooseOp: GremlinTranslationOperator
    {
        public GraphTraversal2 PredicateTraversal;
        public GraphTraversal2 TrueChoiceTraversal;
        public GraphTraversal2 FalseChocieTraversal;
        public GraphTraversal2 ChoiceTraversal;
        public Predicate Predicate;
        public ChooseType Type;
        public Dictionary<object, GraphTraversal2> OptionDict;

        public GremlinChooseOp(GraphTraversal2 traversalPredicate, GraphTraversal2 trueChoice,
            GraphTraversal2 falseChoice)
        {
            PredicateTraversal = traversalPredicate;
            TrueChoiceTraversal = trueChoice;
            FalseChocieTraversal = falseChoice;
            Type = ChooseType.TraversalPredicate;
            OptionDict = new Dictionary<object, GraphTraversal2>();
        }

        public GremlinChooseOp(GraphTraversal2 choiceTraversal)
        {
            ChoiceTraversal = choiceTraversal;
            Type = ChooseType.Option;
            OptionDict = new Dictionary<object, GraphTraversal2>();
        }

        public GremlinChooseOp(Predicate predicate, GraphTraversal2 trueChoice, GraphTraversal2 falseChoice)
        {
            Predicate = predicate;
            TrueChoiceTraversal = trueChoice;
            FalseChocieTraversal = falseChoice;
            Type = ChooseType.Predicate;
            OptionDict = new Dictionary<object, GraphTraversal2>();
        }

        public override GremlinToSqlContext GetContext()
        {
            GremlinToSqlContext inputContext = GetInputContext();

            var chooseExpr = new WChoose2() {ChooseDict = new Dictionary<WScalarExpression, WSqlStatement>()};
            WScalarExpression trueExpr = GremlinUtil.GetColumnReferenceExpression("true");
            WScalarExpression falseExpr = GremlinUtil.GetColumnReferenceExpression("false");

            switch (Type)
            {
                case ChooseType.Predicate:
                    var value = (inputContext.Projection.First().Item2 as ValueProjection).Value;
                    WScalarExpression key = GremlinUtil.GetColumnReferenceExpression(inputContext.CurrVariable.VariableName, value);
                    var predicateExpr = GremlinUtil.GetBooleanComparisonExpr(key, Predicate);
                    chooseExpr.PredicateExpr = predicateExpr;
                    chooseExpr.ChooseDict[trueExpr] = TrueChoiceTraversal.GetEndOp().GetContext().ToSqlQuery();
                    chooseExpr.ChooseDict[falseExpr] = FalseChocieTraversal.GetEndOp().GetContext().ToSqlQuery();
                    break;
                case ChooseType.TraversalPredicate:
                    //Move the context to the choice traversal
                    GremlinUtil.InheritedVariableFromParent(PredicateTraversal, inputContext);
                    chooseExpr.ChooseSqlStatement = PredicateTraversal.GetEndOp().GetContext().ToSqlQuery();

                    //create different branch context
                    GremlinUtil.InheritedVariableFromParent(TrueChoiceTraversal, inputContext);
                    GremlinUtil.InheritedVariableFromParent(FalseChocieTraversal, inputContext);
                    chooseExpr.ChooseDict[trueExpr] = TrueChoiceTraversal.GetEndOp().GetContext().ToSqlQuery();
                    chooseExpr.ChooseDict[falseExpr] = FalseChocieTraversal.GetEndOp().GetContext().ToSqlQuery();
                    break;

                case ChooseType.Option:
                    //Move the context to the choice traversal
                    GremlinUtil.InheritedVariableFromParent(ChoiceTraversal, inputContext);
                    chooseExpr.ChooseSqlStatement = ChoiceTraversal.GetEndOp().GetContext().ToSqlQuery();

                    //create different branch context
                    foreach (var option in OptionDict)
                    {
                        var valueExpr = GremlinUtil.GetValueExpression(option.Key);
                        var optionTraversal = option.Value;

                        GremlinUtil.InheritedVariableFromParent(optionTraversal, inputContext);
                        chooseExpr.ChooseDict[valueExpr] = optionTraversal.GetEndOp().GetContext().ToSqlQuery();
                    }
                    break;
            }
            //Pack the WChoose to a GremlinVariable
            GremlinChooseVariable newVariable = new GremlinChooseVariable(chooseExpr);
            inputContext.AddNewVariable(newVariable, Labels);

            return inputContext;
        }

        public enum ChooseType
        {
            TraversalPredicate,
            Predicate,
            Option
        }

    }
}