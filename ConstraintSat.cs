using System;
using System.Collections;

namespace ConstraintSat
{
	/// <summary>
	/// Implementation of an optimizing constraint satisfaction algorithm.
	/// Determines optimal build scheduling given a set of constraints.
	/// If you want a simple constraint satisfaction solver,
	/// </summary>
	public class ConstraintSat
	{
		// The list of steps
		static Step[] steps;
		static ArrayList domainValues;
		static int evaluations;
		static bool[] timeInUse;

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			DateTime startTime = DateTime.Now;

			// Steps
			steps = new Step[6];
			steps[0] = new Step(0, StepType.Sql, 23);
			steps[1] = new Step(1, StepType.File, 10);
			steps[2] = new Step(2, StepType.Network, 45);
			steps[3] = new Step(3, StepType.Network, 37);
			steps[4] = new Step(4, StepType.Sql, 60);
			steps[5] = new Step(5, StepType.File, 30);

			// Domain Values
			domainValues = DomainValues(steps);

			// Start time variables
			int[] variables = InitVariables();

			// Time in use flags
			timeInUse = new bool[SumLengths(steps)];

			// Remember how much we evaluated
			evaluations = 0;

			// Start the back tracking
			variables = Backtrack(variables);

			// Output evaluation
			Report(variables);

			TimeSpan difference = (DateTime.Now - startTime);
			Console.WriteLine(String.Format("Evaluated {0} (of a possible {1}) leaf nodes in {2} seconds.", 
				evaluations, 
				Math.Pow(domainValues.Count, steps.Length),
				difference.TotalSeconds));
		}

		static void Report(int[] variables)
		{
			Console.WriteLine("Optimal solution: ");
			for(int counter = 0; counter < variables.Length; counter++)
				Console.WriteLine(String.Format("Step {0}: {1}->{2}", counter, variables[counter], variables[counter] + steps[counter].Length));
			Console.WriteLine("Ends at T=" + Evaluate(variables));
			Console.WriteLine();

			int width = 150;
			int totalLength = SumLengths(steps);
			for(int variableNumber = 0; variableNumber < variables.Length; variableNumber++)
			{
				for(int counter = 0; counter < totalLength; counter += (totalLength / width))
				{
					if((counter >= variables[variableNumber]) && (counter < (variables[variableNumber] + steps[variableNumber].Length)))
					{
						if(steps[variableNumber].Type == StepType.File)
							Console.Write("¦");
						if(steps[variableNumber].Type == StepType.Network)
							Console.Write("¦");
						if(steps[variableNumber].Type == StepType.Sql)
							Console.Write("¦");
					}
					else
						Console.Write(" ");
				}
				Console.WriteLine();
			}
			Console.WriteLine("¦ = File\n¦ = Network\n¦ = Sql");	
			Console.WriteLine();
		}

		static int[] Backtrack(int[] variables)
		{
			// If we're at the leaf, verify that we satisfy constraints,
			// and return that solution
			if(AtLeaf(variables))
			{
				evaluations++;
				if(ConstraintsSatisfied(variables))
					return variables;
				else
					return null;
			}

			// Store the best instantiation we've found
			int[] bestInstantiation = InitVariables();

			// Instantiate the variables
			for(int variableNumber = 0; variableNumber < variables.Length; variableNumber++)
			{
				// Skip if the variable has been instantiated
				if(variables[variableNumber] >= 0)
					continue;

				// Prepare the new variable array
				int[] newVariables = (int[]) variables.Clone();

				// Go through the domain of the variable
				// Which is the 0, or aligns with any linear combination of all steps
				foreach(int domainValue in domainValues)
				{
					newVariables[variableNumber] = domainValue;
					if(ConstraintsSatisfied(newVariables))
					{
						int[] backTrackScore = Backtrack(newVariables);

						// General (ie: non-optimizing) constraint satisfaction would have 
						// the following lines, and not store any bestInstantiation.
						// It would also be significantly faster.
						// if(AtLeaf(backTrackScore))
						//	return backTrackScore;

						bestInstantiation = CompareEvaluations(bestInstantiation, backTrackScore);
					}
				}
			}

			return bestInstantiation;
		}

		// At a leaf: all variables are instantiated
		static bool AtLeaf(int[] variables)
		{
			bool atLeaf = true;
			foreach(int variable in variables)
				if(variable == -1)
					atLeaf = false;

			return atLeaf;
		}

		// Create a list of domain values -- 
		// numbers that are valid start times for a step.
		static ArrayList DomainValues(Step[] steps)
		{
			ArrayList domainValues = new ArrayList();

			domainValues.Add(0);
			int combinationCounter = (int) Math.Pow(2, steps.Length);

			// Go through all 2^(steps) combination of steps
			for(int counter = 0; counter < combinationCounter; counter++)
			{
				int domainValue = 0;

				// For all the steps
				for(int currentStep = 0; currentStep < steps.Length; currentStep++)
				{
					// See if this step should be counted
					if((counter & ((int) Math.Pow(2, currentStep))) == Math.Pow(2, currentStep))
					{
						domainValue += steps[currentStep].Length;
					}
				}

				// Keep out duplicates
				if(domainValues.IndexOf(domainValue) < 0)
					domainValues.Add(domainValue);
			}

			return domainValues;
		}

		// Check constraints.  
		// - Always try simplest checks first.
		// - If a constraint involves multiple variables, only test the constraint
		//   when they are initialized
		// - Constrain as much as you can: its reduction in state space is usually worth the perf hit.
		//
		// Note: This method performs the bulk of the work (ie: 90%.) Heavily
		// profile this method and any that it calls!
		static bool ConstraintsSatisfied(int[] variables)
		{
			// Constraint 1: Step 1 after Step 3
			if(! StrictlyAfter(1, 3, variables))
				return false;

			// Constraint 2: Step 2 after Step 4
			if(! StrictlyAfter(2, 4, variables))
				return false;

			// Constraint 3: Step 5 starts at t=70
			// Note: be careful not to introduce mandated idle time, because that will cause
			// IdleTimeExists to fail.
			// A workaround could be to introduce a "ForcedIdle" job type.
			if((variables[5] >= 0) && (variables[5] != 70))
				return false;

			// Constraint 3: No jobs of the same type can overlap
			// This is an O(n^2) algorithm (in # of steps,) but the setup cost
			// and infrastructure of sorting by time is likely more expensive.
			if(StepTypesOverlap(variables))
				return false;

			// Constraint 4: No idle time
			if(IdleTimeExists(variables))
				return false;

			return true;
		}

		// We're not investigating an optimal solution if there is 
		// idle time between steps.
		static bool IdleTimeExists(int[] variables)
		{
			bool idleTimeExists = false;

			UpdateTimeInUse(variables, true);

			// Work backwards, making sure we don't find any gaps
			bool foundTimeInUse = false;
			for(int currentTime = timeInUse.Length - 1; currentTime >= 0; currentTime--)
			{
				foundTimeInUse |= timeInUse[currentTime];
				if(foundTimeInUse)
				{
					if(! timeInUse[currentTime])
					{
						idleTimeExists = true;
						break;
					}
				}
			}

			UpdateTimeInUse(variables, false);

			return idleTimeExists;
		}

		static void UpdateTimeInUse(int[] variables, bool updateValue)
		{
			// Set our array of "time in use" to false if it was true.
			int variableLen = variables.Length;
			for(int currentVariable = 0; currentVariable < variableLen; currentVariable++)
			{
				int startTime = variables[currentVariable];
				if(startTime == -1)
					continue;

				int stepLength = steps[currentVariable].Length + startTime;
				for(int progress = startTime; progress < stepLength; progress++)
					timeInUse[progress] = updateValue;
			}
		}

		/// <summary>
		/// Test if firstStep is strictly after secondStep.
		/// That is, firstStep starts after secondStep ends.
		/// </summary>
		static bool StrictlyAfter(int firstStep, int secondStep, int[] variables)
		{
			if(
				(variables[firstStep] >= 0) &&
				(variables[secondStep] >= 0))
			{
				if(variables[firstStep] < (steps[secondStep].Length + variables[secondStep]))
					return false;
			}

			return true;
		}

		static bool StepTypesOverlap(int[] variables)
		{
			for(int firstStep = 0; firstStep < steps.Length; firstStep++)
			{
				for(int secondStep = 0; secondStep < steps.Length; secondStep++)
				{
					if(firstStep == secondStep)
						continue;

					if((variables[firstStep] == -1) || (variables[secondStep] == -1))
						continue;

					// If they aren't the same type, skip
					if(steps[firstStep].Type != steps[secondStep].Type)
						continue;

					// They are the same type, see if they overlap
					// 1) SecondStep starts while FirstStep is running
					if(
						(variables[secondStep] >= variables[firstStep]) &&
						(variables[secondStep] < (variables[firstStep] + steps[firstStep].Length))
						)
						return true;
					// 2) FirstStep starts while SecondStep is running
					if(
						(variables[firstStep] >= variables[secondStep]) &&
						(variables[firstStep] < (variables[secondStep] + steps[secondStep].Length))
						)
						return true;
				}
			}

			return false;
		}

		static int[] CompareEvaluations(int[] bestInstantiation, int[] backTrackScore)
		{
			int[] returnVariables = new int[backTrackScore.Length];

			if(backTrackScore == null)
				return bestInstantiation;

			if(Evaluate(backTrackScore) < Evaluate(bestInstantiation))
				return (int[]) backTrackScore.Clone();
			else
				return (int[]) bestInstantiation.Clone();
		}

		// Evaluation function: see where we end
		static int Evaluate(int[] variables)
		{
			int latestEndTime = Int32.MinValue;

			foreach(Step currentStep in steps)
			{
				if(variables[currentStep.StepId] == -1)
					return Int32.MaxValue;

				int stepStart = variables[currentStep.StepId];
				int endTime = stepStart + currentStep.Length;

				if(endTime > latestEndTime)
					latestEndTime = endTime;
			}

			return latestEndTime;
		}

		static int[] InitVariables()
		{
			// All variables are un-bound
			int[] variables = new int[steps.Length];
			for(int counter = 0; counter < steps.Length; counter++)
				variables[counter] = -1;

			return variables;
		}

		static int SumLengths(Step[] steps)
		{
			int endToEndDuration = 0;
			int maxLength = 0;

			foreach(Step step in steps)
			{
				endToEndDuration += step.Length;
				if(step.Length > maxLength) maxLength = step.Length;
			}

			return (endToEndDuration + maxLength);
		}
	}

	class Step
	{
		private int Id;
		private StepType stepType;
		private int stepLength;

		public Step(int Id, StepType stepType, int stepLength)
		{
			this.Id = Id;
			this.stepType = stepType;
			this.stepLength = stepLength;
		}

		public int Length { get { return stepLength; } }
		public int StepId { get { return this.Id; } }
		public StepType Type { get { return stepType; } }
	}

	public enum StepType { File, Network, Sql };
}