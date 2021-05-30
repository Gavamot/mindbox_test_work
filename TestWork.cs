using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FiguresDotStore.Controllers
{
	// Вообще на такое лучше бы написать тесты.
	// Но в целях экономии времени и недостатка информации по бизнес логике.
	// Воздержусь от этого скорее всего в коде будут ошибки из-за их отсутствия

	//  **************** DTO 
	public class PositionDto
	{
		public double SideA { get; set; }
		public double SideB { get; set; }
		public double SideC { get; set; }
		public FigureTypes Type { get; set; } // Возможно нужно будет настроить сериализатор чтобы отдавал строки 
		public int Count { get; set; }

		// Фабричный метод можно вынести в mapper или использовать automapper
		public OrderPosition ToOrderPosition()
		{
			var figure = Type.GetFigure();
			figure.SideA = SideA;
			figure.SideB = SideB;
			figure.SideC = SideC;

			return new OrderPosition()
			{
				Figure = figure,
				Count = Count
			};
		}
	}

	public class CartDto
	{
		// Это спорно и зависит от бизнес логики но я думаю неизменяемый тип данных здесь будет уместнее
		public PositionDto[] Positions { get; set; } = Array.Empty<PositionDto>();
		// Фабричный метод можно вынести в mapper или использовать automapper
		public Order ToOrder()
		{
			var res = new Order
			{
				Positions = Positions.Select(p => p.ToOrderPosition()).ToArray()
			};
			return res;
		}
	}

	// ***************** Models (Условно тут кто как называет)

	public static class FigureTypesExt
	{
		public static Figure GetFigure(this FigureTypes figure)
		{
			switch (figure)
			{
				case FigureTypes.Triangle: return new Triangle();
				case FigureTypes.Square: return new Square();
				case FigureTypes.Circle: return new Circle();
				default: throw new NotImplementedException($"Have no implementation for cast figureType={figure} -> Figure");
			}
		}
	}

	public enum FigureTypes
	{
		Triangle,
		Square,
		Circle
	}

	public abstract class Figure
	{
		public double SideA { get; set; }
		public double SideB { get; set; }
		public double SideC { get; set; }
		public abstract FigureTypes Type { get; }
		public abstract double GetArea(); // Тут нужно указать единицу измерения
		public abstract double GetTotal(); // Нужно переименовать в чтони-будь понятное
		public abstract bool IsInvalid(); // Не знаю ничего за архитектуру данного проекта. Но думаю что вальвацию лучше бы делать с помощью например fluent assertion

	}

	public class Triangle : Figure
	{
		public override FigureTypes Type => FigureTypes.Triangle;

		public override bool IsInvalid()
		{
			bool CheckTriangleInequality(double a, double b, double c) => a.CompareTo(b + c) <= 0; // В числах с плавающей точкой есть погрешность. Еще поменял условие на обратное чтобы удобнее сравнивать 
			return CheckTriangleInequality(SideA, SideB, SideC)
				&& CheckTriangleInequality(SideB, SideA, SideC)
				&& CheckTriangleInequality(SideC, SideB, SideA);
		}

		public override double GetArea()
		{
			var p = (SideA + SideB + SideC) / 2.0;
			return Math.Sqrt(p * (p - SideA) * (p - SideB) * (p - SideC));
		}

		public override double GetTotal() =>
			 GetArea() * 1.2; // Тут бы лучше 1.2 назвать както чем использовать magic number но у меня нет контекста поэтому не могу дать нормальное название коэффициенту
	}

	public class Square : Figure
	{
		public override FigureTypes Type => FigureTypes.Square;
		public override bool IsInvalid()
		{
			if (SideA < 0) return true;
			return SideA != SideB;
		}
		public override double GetArea() => SideA * SideA;
		public override double GetTotal() => GetArea() * 0.9;
	}

	public class Circle : Figure
	{
		public override FigureTypes Type => FigureTypes.Circle;
		public override bool IsInvalid() => SideA < 0;

		public override double GetArea() => Math.PI * SideA * SideA;

		public override double GetTotal() => 0; // Тут скорее всего у того кто писал была ошика надо уточнять коэффициент пока 0 так как в коде нигде не учитывается
	}

	public interface IFigurePosition
    {
		FigureTypes Type { get; }
		int Count { get; set; }
	}

	public class OrderPosition : IFigurePosition
	{
		public override int GetHashCode()
		{
			return (int)Figure.Type;
		}

		public override bool Equals(object obj)
		{
			var o = obj as OrderPosition;
			if (o == default) return false;
			return o.Figure.Type == Figure.Type;
		}

		public Figure Figure { get; set; }
		public int Count { get; set; }
		public bool IsInvalid() => Figure.IsInvalid();
		public FigureTypes Type => Figure.Type;
	}

	public class Order
	{
		// Я незнаю бизнес логики возможно тут более актуален массив или readonlyList
		public OrderPosition[] Positions { get; set; } = Array.Empty<OrderPosition>();

		public bool IsValid() => Positions.Any(p => p.IsInvalid());

		public double GetTotal() => Positions.Sum(p => p.Figure.GetArea());
	}

	// Сервисы

	public class Transaction : IDisposable
    {
		private bool isCommited = false;
		public void Rollback()
        {
			// Откатываем
        }

		public void Commit()
        {
			isCommited = true;
        }

        public void Dispose()
        {
            if (!isCommited)
            {
				Rollback(); 
            }
        }
    }

	// Редиска скорее всего будет на разных нодах и вызовы по сети могут занимать десятки миллисекунд. Поэтому нужны асинхронные вызовы
	// Также скорее под большой нагрузкой нужен пул соединений
	public interface IRedisClient : IDisposable // Скорее всего его забыли так как клиента следует вернуть в poll соединений
	{
		Task<Dictionary<K, V>> GetBatchAsync<K, V>(IEnumerable<K> keys); // Тут нужно чтение пачкой
		Task SetBatchAsync<K, V>(IEnumerable<KeyValuePair<K, V>> keyValue); // Тут вставим партию записей одной пачкой
		Transaction BeginTransaction();
    }

	public interface IOrderStorage
	{
		Task<double> SaveAsync(Order order);
	}

	public interface IFiguresStorage
	{
		Task Unreserve(IFigurePosition[] positions);
		Task Reserve(IFigurePosition[] order);
	}

	public class IsNotEnoughFiguresException : Exception {
		public IsNotEnoughFiguresException()
        {
		
		}
	}

	public class OrderHandlingException : Exception
	{
		public OrderHandlingException(Exception e) : base("Can not handle an order", e)
		{

		}
	}

	public class FiguresStorage : IFiguresStorage
	{
		// корректно сконфигурированный и готовый к использованию клиент Редиса
		readonly IRedisClient redis;
		public FiguresStorage(IRedisClient redis)
        {
			this.redis = redis;
        }

		public async Task Unreserve(IFigurePosition[] positions)
        {
			var unicPositions = GetUnicPositions(positions);
			var restOfFigureTypes = await redis.GetBatchAsync<FigureTypes, int>(unicPositions.Keys);
			var reservePositions = restOfFigureTypes.Select(x => new KeyValuePair<FigureTypes, int>(x.Key, x.Value + unicPositions[x.Key].Count));
			await redis.SetBatchAsync(reservePositions);
		}


		/// <exception cref="IsNotEnoughFiguresException"></exception>
		public async Task Reserve(IFigurePosition[] positions)
        {
			using var transaction = redis.BeginTransaction();
			
			// Позиции могут быть не уникальными поэтому резервироваться только количество 
			var unicPositions = GetUnicPositions(positions);
			var figureTypes = unicPositions.Select(x => x.Key);

			var restOfFigureTypes = await redis.GetBatchAsync<FigureTypes, int>(figureTypes);
			ThowIfNotEnoughFigures(positions, restOfFigureTypes);

			var reservePositions = restOfFigureTypes.Select(x => new KeyValuePair<FigureTypes, int>(x.Key, x.Value - unicPositions[x.Key].Count));
			await redis.SetBatchAsync(reservePositions);

			transaction.Commit();

			// Если производительность совсем критична можно обойтись без транзакции тогда придется добавить погда придется проверить
			// после списания остатков что они не отрицательные если станут отрицательными откатить действие добавил позиции назад 
		}

		private Dictionary<FigureTypes, IFigurePosition> GetUnicPositions(IFigurePosition[] positions)
		{
			Dictionary<FigureTypes, IFigurePosition> res = new();
			foreach (var p in positions)
			{
				if (res.ContainsKey(p.Type))
				{
					res[p.Type].Count += p.Count;
				}
				else
				{
					res.Add(p.Type, p);
				}
			}
			return res;
		}

		private void ThowIfNotEnoughFigures(IFigurePosition[] positions, Dictionary<FigureTypes, int> rest)
		{
			foreach (var p in positions)
			{
				var need = positions.First(x => x.Type == p.Type).Count;
				if (!rest.ContainsKey(p.Type)) throw new IsNotEnoughFiguresException();
				if ((rest[p.Type] - need) < 0) throw new IsNotEnoughFiguresException();
			}
		}
    }

	// Если случится сбой например упадет редька то надо сделать какой нибудь механизм чтобы эти проблемы можно было решить в ручную либо автоматом когда все восстановится
	public interface IOrderProblemsResolver
    {
		Task ResolveProblem(Order oreder, Exception e); // Запишем в лог либо бд инфу - Тут надо продумывать 
    }

	public interface IOrderServise
	{
		Task<double> SaveAsync(Order order);
	}

	public class OrderService : IOrderServise
	{
		readonly IFiguresStorage figuresStorage;
		readonly IOrderStorage orderStorage;
		readonly IOrderProblemsResolver orderProblemsResolver;

		public OrderService(IOrderStorage orderStorage, IFiguresStorage figuresStorage, IOrderProblemsResolver orderProblemsResolver)
        {
			this.orderStorage = orderStorage;
			this.figuresStorage = figuresStorage;
			this.orderProblemsResolver = orderProblemsResolver;
        }

		/// <exception cref="IsNotEnoughFiguresException"></exception>
		/// <exception cref="OrderHandlingException"></exception>
		public async Task<double> SaveAsync(Order order)
        {
			await figuresStorage.Reserve(order.Positions);
            try
            {
				return await orderStorage.SaveAsync(order);
			}
            catch(Exception e)
            {
				await RollbackReserve(order, e);
				throw new OrderHandlingException(e); 
            }
        }

		private async Task RollbackReserve(Order order, Exception ex)
        {
			// Все плохо мы изменили остатки но не можем сохранить заказ
			// Пробуем откатится. Если не получилось придется админам решать это в ручную либо писать отдельный механизм
			try
			{
				await figuresStorage.Unreserve(order.Positions);
			}
			catch
			{
				await orderProblemsResolver.ResolveProblem(order, ex);
			}
		}
	}

	[ApiController]
	[Route("[controller]")]
	public class FiguresController : ControllerBase
	{
		// не знаю какой стиль наименования принят на проекте с _ для приватных полей или нет. Я пока уберу так как больше привык без них но если он принят с ними то нужно их добавить
		private readonly ILogger<FiguresController> log;
		private readonly IOrderServise orderService;

		public FiguresController(ILogger<FiguresController> log, IOrderServise orderService)
		{
			this.log = log;
			this.orderService = orderService;
		}

		// хотим оформить заказ и получить в ответе его стоимость
		[HttpPost]
		public async Task<ActionResult<double>> Order(CartDto cart)
		{
			Order order = new();
			try
            {
				order = cart.ToOrder();
				var res = await orderService.SaveAsync(order);
				return res;
			}
			catch (NotImplementedException e) // Лучше вести обработку исключений в middle-ware тут для примера
			{
				// Подобное логирование лучше вести на уровне middle-ware а тут просто выкидывать ошибку. Поскольку тут не прод залогирую тут
				log.LogError(e.Message, e);
				return StatusCode(StatusCodes.Status500InternalServerError, e.Message); // В зависимости от бизнес логики и архитектуры проекта ошибка может быть другой
			}catch (IsNotEnoughFiguresException e)
			{
				log.LogError("Not enough .... order {order} ", e, order);
				return StatusCode(StatusCodes.Status500InternalServerError, e.Message); // ! Текст ошибки на клиента отправляется для примера это не самая лучшая практика хотя для enterprice приложения норм
			}
			catch (OrderHandlingException e)
			{
				log.LogError("Error order handling ....  order {order}", e, order);
				return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
			}
			catch (Exception e)
			{
				log.LogError("Error", e);
				return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
			}
		}
	}
}
