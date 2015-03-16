# ConstraintSatisfaction

Implementation of constraint satisfaction algorithm for optimizing multi-resource build schedules.

We used this as the basis of a build optimization process we used for Encarta Online.

The goal with this implementation is that you might have a build or deployment that requires
many tasks, with dependencies among themselves. Each task might conceptually be grouped into
to a "type" of resource: SQL stages, file processing stages, network copy stages, etc.

One challenge is that any resource (i.e.: network copy) can only efficiently process one task of
its type at a time. So you might prefer to do a seemingly out-of-order network copy early
in the process if it enables the processing of a large tree of unrelated work that depends on it.

Constraint Satisfaction (http://en.wikipedia.org/wiki/Constraint_satisfaction) is the basis
of the technique that we used to help meet these constraints to create a build schedule that was
as short as possible.