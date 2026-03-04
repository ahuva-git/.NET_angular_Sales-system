


using AutoMapper;
using WebApiProject.Models;
using WebApiProject.Models.DTO;

namespace WebApiProject
{
    /// <summary>
    /// פרופיל AutoMapper - מגדיר את כל המיפויים בין Entities ל-DTOs
    /// AutoMapper משמש להמרה אוטומטית בין אובייקטים, חוסך קוד ידני
    /// כל המיפויים נטענים אוטומטית בעת אתחול האפליקציה
    /// </summary>
    public class Profile : AutoMapper.Profile
    {
        /// <summary>
        /// קונסטרקטור שמגדיר את כל המיפויים בין Models ל-DTOs
        /// נקרא אוטומטית ע"י AutoMapper בעת הרצת האפליקציה
        /// </summary>
        public Profile()
        {
            // ============================================================
            // כללים כלליים למיפויים:
            // 1. ID תמיד מתעלם (Ignore) כי הוא מוגדר כ-PK אוטומטי ב-DB
            // 2. Navigation Properties (כמו User, Gift) מתעלמים בכיוון DTO→Entity
            // 3. שדות מחושבים ממופים בכיוון Entity→DTO בלבד
            // ============================================================

            // ===== Donor (תורמים) =====            // ===== Donor (תורמים) =====
            
            // DTO → Entity (לפעולות Add/Update)
            CreateMap<DonorDTO, Donor>()
                .ForMember(d => d.Id, opt => opt.Ignore());  // ID נוצר אוטומטי ב-DB

            // Entity → DTO (לפעולות Get)
            CreateMap<Donor, DonorDTO>();

            // Entity → GetDTO (לפעולות Get עם פרטים מלאים)
            CreateMap<Donor, DonorGetDTO>();

            // GetDTO → Entity (לעדכונים מיוחדים)
            CreateMap<DonorGetDTO, Donor>()
                .ForMember(g => g.Id, opt => opt.Ignore());  // מתעלם מ-ID


            // ===== Gift (מתנות) =====
            
            // Entity → DTO (לפעולות Get)
            CreateMap<Gift, GiftDTO>()
                .ForMember(dest => dest.DonorName,              // ממפה שם תורם מאובייקט מקונן
                    opt => opt.MapFrom(src => src.Donor.Name)); // Navigation Property
            // הערה: IsRaffled ממופה אוטומטית (שם זהה בשני האובייקטים)

            // DTO → Entity (לפעולות Add)
            CreateMap<GiftDTO, Gift>()
                .ForMember(g => g.Id, opt => opt.Ignore());          // ID נוצר אוטומטי

            // Entity → GetDTO (לפעולות Get מפורטות)
            CreateMap<Gift, GiftGetDTO>();

            // GetDTO → Entity (לעדכונים)
            CreateMap<GiftGetDTO, Gift>()
                .ForMember(g => g.Id, opt => opt.Ignore());          // מתעלם מ-ID

            // DTO → Entity (מיפוי נוסף עם התעלמות מרכישות)
            CreateMap<GiftDTO, Gift>()
                .ForMember(dest => dest.Shoppings, opt => opt.Ignore());  // Navigation Property


          
            // ===== Shopping (רכישות) =====
            
            // DTO → Entity (לפעולות Add/Update)
            CreateMap<ShoppingDTO, Shopping>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())      // ID נוצר אוטומטי
                .ForMember(dest => dest.User, opt => opt.Ignore())    // Navigation Property - לא ממפים
                .ForMember(dest => dest.Gift, opt => opt.Ignore());   // Navigation Property - לא ממפים

            // Entity → DTO (לפעולות Get) ⭐ חשוב ביותר!
            CreateMap<Shopping, ShoppingDTO>()
                // ממפה פרטי משתמש מאובייקט מקונן User
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User.UserName))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.User.Email))
                .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.User.Phone));
            // הערה: IsDraft, UserId, GiftId, Quantity ממופים אוטומטית (שמות זהים)

            // ===== User (משתמשים) =====
            
            // Entity → DTO (לפעולות Get)
            CreateMap<User, UserDTO>();

            // DTO → Entity (לפעולות Add/Update)
            CreateMap<UserDTO, User>()
                .ForMember(u => u.Id, opt => opt.Ignore())  // ID נוצר אוטומטי
                // PasswordHash ממופה רק אם Password לא ריק (למקרה של עדכון סיסמה)
                .ForMember(u => u.PasswordHash, 
                    opt => opt.Condition(src => !string.IsNullOrEmpty(src.Password)));

        }
    }
}
