﻿namespace NServiceBus.Unicast.Tests
{
    using System;
    using System.Linq;
    using Core.Tests;
    using NUnit.Framework;
    using UnitOfWork;

    [TestFixture]
    public class UnitOfWorkRunnerTests
    {
        [Test]
        public void When_first_throw_second_is_cleaned_up()
        {
            var builder = new FuncBuilder();
            
            var unitOfWorkThatThrowsFromEnd = new UnitOfWorkThatThrowsFromEnd();
            var unitOfWork = new UnitOfWork();

            builder.Register<IManageUnitsOfWork>(() => unitOfWorkThatThrowsFromEnd);
            builder.Register<IManageUnitsOfWork>(() => unitOfWork);
            var runner = new UnitOfWorkRunner
                         {
                             Builder = builder
                         };
            runner.Begin();
            Assert.Throws<InvalidOperationException>(runner.End);
            Assert.IsTrue(unitOfWorkThatThrowsFromEnd.BeginCalled);
            Assert.IsTrue(unitOfWorkThatThrowsFromEnd.EndCalled);
            Assert.IsTrue(unitOfWork.BeginCalled);
            Assert.IsTrue(unitOfWork.EndCalled);
        }

        [Test]
        public void Should_append_end_exception_to_rethrow()
        {
            var builder = new FuncBuilder();

            var unitOfWork = new UnitOfWorkThatThrowsFromEnd();

            builder.Register<IManageUnitsOfWork>(() => unitOfWork);
            var runner = new UnitOfWorkRunner
            {
                Builder = builder
            };
            var exception = new Exception();
            runner.Begin();
            var aggregateException = Assert.Throws<AggregateException>(() => runner.AppendEndExceptionsAndRethrow(exception));
            var innerExceptions = aggregateException.InnerExceptions;
            Assert.AreSame(exception, innerExceptions[0]);
            Assert.AreSame(unitOfWork.ExceptionThrownFromEnd, innerExceptions[1]);
        }

        public class UnitOfWorkThatThrowsFromEnd : IManageUnitsOfWork
        {
            public bool BeginCalled;
            public bool EndCalled;
            public Exception ExceptionThrownFromEnd = new InvalidOperationException();

            public void Begin()
            {
                BeginCalled = true;
            }

            public void End(Exception ex = null)
            {
                EndCalled = true;
                throw ExceptionThrownFromEnd;
            }

        }

        public class UnitOfWork : IManageUnitsOfWork
        {
            public bool BeginCalled;
            public bool EndCalled;

            public void Begin()
            {
                BeginCalled = true;
            }

            public void End(Exception ex = null)
            {
                EndCalled = true;
            }
        }

        [Test]
        public void Verify_order()
        {
            var builder = new FuncBuilder();

            var unitOfWork1 = new CountingUnitOfWork();
            var unitOfWork2 = new CountingUnitOfWork();
            var unitOfWork3 = new CountingUnitOfWork();

            builder.Register<IManageUnitsOfWork>(() => unitOfWork1);
            builder.Register<IManageUnitsOfWork>(() => unitOfWork2);
            builder.Register<IManageUnitsOfWork>(() => unitOfWork3);
            var runner = new UnitOfWorkRunner
                         {
                             Builder = builder
                         };
            runner.Begin();
            runner.End();

            Assert.AreEqual(1, unitOfWork1.BeginCallIndex);
            Assert.AreEqual(2, unitOfWork2.BeginCallIndex);
            Assert.AreEqual(3, unitOfWork3.BeginCallIndex);
            Assert.AreEqual(3, unitOfWork1.EndCallIndex);
            Assert.AreEqual(2, unitOfWork2.EndCallIndex);
            Assert.AreEqual(1, unitOfWork3.EndCallIndex);
        }

        public class CountingUnitOfWork : IManageUnitsOfWork
        {
            static int BeginCallCount;
            static int EndCallCount;
            public int EndCallIndex;
            public int BeginCallIndex;

            public void Begin()
            {
                BeginCallCount++;
                BeginCallIndex = BeginCallCount;
            }
            public void End(Exception ex = null)
            {
                EndCallCount++;
                EndCallIndex = EndCallCount;
            }
        }

        [Test]
        public void Should_pass_exception_to_cleanup()
        {
            var builder = new FuncBuilder();

            var unitOfWork = new CaptureExceptionPassedToEndUnitOfWork();

            builder.Register<IManageUnitsOfWork>(() => unitOfWork);
            var runner = new UnitOfWorkRunner
                         {
                             Builder = builder
                         };
            var exception = new Exception();
            runner.Begin();
            var aggregateException = Assert.Throws<AggregateException>(() => runner.AppendEndExceptionsAndRethrow(exception));
            Assert.AreSame(exception, unitOfWork.Exception);
            Assert.AreSame(exception, aggregateException.InnerExceptions.Single());
        }

        public class CaptureExceptionPassedToEndUnitOfWork : IManageUnitsOfWork
        {
            public void Begin()
            {
            }
            public void End(Exception ex = null)
            {
                Exception = ex;
            }
            public Exception Exception;
        }
    }
}