import { Component, inject } from '@angular/core';
import { GiftService } from '../../../services/gift-service';
import { GiftModel } from '../../../models/gift-model';
import { GiftFilterDTO, GiftSortBy } from '../../../models/gift-filter.model';
import { RaffleResultDTO } from '../../../models/raffle-result-model';
import { Gift } from '../gift/gift';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ShoppingService } from '../../../services/shopping-service';
import { ShoppingCreateModel } from '../../../models/shopping/ShoppingCreate -model';
import { AuthService } from '../../../services/auth-service';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';

@Component({
  selector: 'app-gifts-list',
  standalone: true,
  imports: [Gift, CommonModule, FormsModule],
  templateUrl: './gifts-list.html',
  styleUrls: ['./gifts-list.scss'],
})

export class GiftsList {
  giftSrv = inject(GiftService);
  shoppingSrv = inject(ShoppingService);
  authSrv = inject(AuthService);
  router = inject(Router);

   // משתני רכיב
  gifts$: Observable<GiftModel[]> = this.giftSrv.getAllGifts();
  id: number = -1; // קוד מתנה לעריכה
  errorMsg: string = ''; // הודעת שגיאה
  raffleResult: RaffleResultDTO[] = []; // תוצאות הגרלה אחרונות
  
  // סינון ומיון
  filter: GiftFilterDTO = {
    giftName: '',
    donorName: '',
    category: '',
    sortBy: undefined,
    desc: false
  };

  // Enum for template access
  GiftSortBy = GiftSortBy;

  ngOnInit() {
    // טעינת זוכים מהשרת (נתונים אמיתיים מבסיס הנתונים)
    this.loadWinnersFromServer();
  }

  // טעינת זוכים מהשרת
  private loadWinnersFromServer() {
    this.giftSrv.getAllWinners().subscribe({
      next: (winners: RaffleResultDTO[]) => {
        this.raffleResult = winners;
        console.log('Loaded winners from server:', winners);
      },
      error: (err) => {
        console.error('Error loading winners from server:', err);
        this.errorMsg = 'שגיאה בטעינת זוכים מהשרת';
        this.raffleResult = [];
      }
    });
  }

  // טעינת מתנות מהשרת
  loadGifts() {
    this.gifts$ = this.giftSrv.getAllGifts();
  }

  // הוספת מתנה לסל קניות
  addToCart(giftId: number) {
    if (!this.authSrv.isLoggedIn()) {
      this.router.navigate(['/login']);
      return;
    }

    this.errorMsg = '';
    const userId = this.authSrv.getUserIdFromToken();

    // קבל את כל הרכישות כדי לבדוק אם כבר קיימת
    this.shoppingSrv.getAllShoppings().subscribe({
      next: (allShoppings: any[]) => {
        
        // חפש רכישה קיימת של אותה מתנה וגם טיוטה
        const existingDraft = allShoppings.find((s: any) => {
          const isDraftValue = s.isDraft !== undefined ? s.isDraft : true;
          return s.userId === userId && s.giftId === giftId && isDraftValue === true;
        });

        if (existingDraft) {
          // אם קיימת - עדכן את הכמות
          existingDraft.quantity++;
          
          this.shoppingSrv.updateShopping(existingDraft).subscribe({
            next: () => {
              //console.log('Shopping updated successfully');
              setTimeout(() => this.router.navigate(['/shoppings']), 500);
            },
            error: (err) => {
              console.error('Error updating shopping:', err);
              try {
                // Try to parse if it's a JSON string
                const errorObj = typeof err.error === 'string' ? JSON.parse(err.error) : err.error;
                
                if (errorObj?.errors && Array.isArray(errorObj.errors) && errorObj.errors.length > 0) {
                  this.errorMsg = 'Error updating purchase: ' + errorObj.errors[0].message;
                } else if (errorObj?.message) {
                  this.errorMsg = 'Error updating purchase: ' + errorObj.message;
                } else if (typeof err.error === 'string') {
                  this.errorMsg = 'Error updating purchase: ' + err.error;
                } else {
                  this.errorMsg = 'Error updating purchase';
                }
              } catch {
                this.errorMsg = 'Error updating purchase: ' + err.error;
              }
            }
          });
        } else {
          // אם לא קיימת טיוטה - צור חדשה
          const dataToSend: ShoppingCreateModel = {
            userId: userId,
            giftId: giftId,
            quantity: 1
          };
                    
          this.shoppingSrv.addShopping(dataToSend).subscribe({
            next: (response) => {
              console.log('Shopping created successfully:', response);
              setTimeout(() => this.router.navigate(['/shoppings']), 500);
            },
            error: (err) => {
              //console.error('Error adding shopping:', err);
              this.handleAddToCartError(err);
            }
          });
        }
      },
      error: (err) => {
        console.error('Error fetching shoppings:', err);
        this.errorMsg = 'Error loading shoppings: ' + err.error;
      }
    });
  }

  // פונקציה עזר לטיפול בשגיאות
  private handleAddToCartError(err: any) {
    console.error('Add to cart error:', err.error);
    let errorMessage = 'Error adding to cart';
    
    if (typeof err.error === 'string') {
      errorMessage = err.error;
    } else if (err.error?.errors && Array.isArray(err.error.errors)) {
      const errorsArray = err.error.errors;
      const errorMessages = errorsArray.map((e: any) => {
        if (typeof e === 'object' && e.message) {
          return e.message;
        }
        return typeof e === 'string' ? e : JSON.stringify(e);
      });
      errorMessage = errorMessages.join(' | ');
    } else if (err.error?.title) {
      errorMessage = err.error.title;
    } else if (err.error?.message) {
      errorMessage = err.error.message;
    }
    this.errorMsg = errorMessage;
  }

  applyFilters() {
    this.errorMsg = '';
    const filterDTO: GiftFilterDTO = {};
    
    if (this.filter.giftName?.trim()) filterDTO.giftName = this.filter.giftName.trim();
    if (this.filter.donorName?.trim()) filterDTO.donorName = this.filter.donorName.trim();
    if (this.filter.category?.trim()) filterDTO.category = this.filter.category.trim();
    if (this.filter.sortBy) {
      filterDTO.sortBy = this.filter.sortBy;
      filterDTO.desc = this.filter.desc;
    }

    // אם יש לפחות סינון או מיון אחד, השתמש ב-API מסונן
    if (Object.keys(filterDTO).length > 0) {
      this.gifts$ = this.giftSrv.getFilteredGifts(filterDTO);
    } else {
      // אחרת, הצג את כל המתנות
      this.loadGifts();
    }
  }

  clearFilters() {
    this.filter = { 
      giftName: '', 
      donorName: '', 
      category: '', 
      sortBy: undefined, 
      desc: false 
    };
    this.loadGifts();
  }

  // אירוע בשמירת מתנה
  onGiftSaved(newId: number) {
    this.id = newId;
    this.loadGifts();
  }

  // הוספת מתנה חדשה
  addGift(gift: GiftModel) {
    this.errorMsg = '';
    this.giftSrv.addGift(gift).subscribe({
      next: () => {
        this.loadGifts();
      },
      error: (err) => {
        if (err.error?.message) {
          this.errorMsg = err.error.message;
        } else if (err.error?.errors && Array.isArray(err.error.errors) && err.error.errors.length > 0) {
          this.errorMsg = err.error.errors[0].message;
        } else if (typeof err.error === 'string') {
          this.errorMsg = err.error;
        } else if (err.message) {
          this.errorMsg = err.message;
        } else {
          this.errorMsg = 'Error adding gift';
        }
      }
    });
  }

  // מחיקת מתנה
  removeGift(giftID: number) {
    this.errorMsg = '';
    this.giftSrv.removeGift(giftID).subscribe({
      next: () => {
        this.loadGifts();
      },
      error: (err) => {
        let errorMessage = 'Error deleting gift';
        
        if (typeof err.error === 'string' && err.error.length > 0) {
          errorMessage = err.error;
        } else if (err.error?.message) {
          errorMessage = err.error.message;
        } else if (err.error?.detail) {
          errorMessage = err.error.detail;
        } else if (err.error?.title) {
          errorMessage = err.error.title;
        } else if (err.message) {
          errorMessage = err.message;
        }
        this.errorMsg = errorMessage;
      }
    });
  }

  // עריכת מתנה - פתיחת טופס עריכה
  updateGift(giftID: number) {
    this.errorMsg = '';
    this.id = giftID;
  }

  // בדיקה אם המשתמש הוא מנהל
  isManager(): boolean {
    return this.authSrv.isManager();
  }

  raffleGift(giftId: number) {
    this.errorMsg = '';
    this.giftSrv.raffleGift(giftId).subscribe({
      next: (res: RaffleResultDTO) => {
        alert(`🎉 The winner of gift "${res.giftName}" is: ${res.winnerUserName}`);
        // טעינה מחדש של זוכים מהשרת כדי לקבל נתונים עדכניים
        this.loadWinnersFromServer();
        this.loadGifts();
      },
      error: (err) => {
        if (err.error?.message) {
          this.errorMsg = err.error.message;
        } else if (err.error?.errors && Array.isArray(err.error.errors) && err.error.errors.length > 0) {
          this.errorMsg = err.error.errors[0].message;
        } else if (typeof err.error === 'string') {
          this.errorMsg = err.error;
        } else if (err.message) {
          this.errorMsg = err.message;
        } else {//שגיאה בביצוע ההגרלה למתנה
          this.errorMsg = 'Error performing raffle for gift';
        }
      }
    });
  }
  
  // downloadPdf() {
  //   this.giftSrv.getRaffleWinnersPdf().subscribe({
  //     next: (blob: Blob) => {
  //       const url = window.URL.createObjectURL(blob);
  //       window.open(url);
  //     },
  //     error: (err) => {
  //       console.error('PDF Download Error:', err);
  //       if (err.status === 401) {
  //         this.errorMsg = 'אין הרשאה להורדת דוח - יש להתחבר כמנהל';
  //       } else if (err.status === 0) {
  //         this.errorMsg = 'שגיאת תקשורת עם השרת - ודא ש-CORS מוגדר';
  //       } else {
  //         this.errorMsg = `שגיאה בהורדת דוח הזוכים (${err.status})`;
  //       }
  //     }
  //   });
  // }

  downloadPdf() {
    this.giftSrv.getRaffleWinnersPdf()
      .subscribe({
        next: (blob: Blob) => {
          // Create temporary URL for the blob
          const url = window.URL.createObjectURL(blob);
          
          // Create invisible download link
          const a = document.createElement('a');
          a.href = url;
          a.download = `raffle-winners-${new Date().toISOString().split('T')[0]}.pdf`;
          
          // Trigger download
          document.body.appendChild(a);
          a.click();
          
          // Clean up
          document.body.removeChild(a);
          window.URL.revokeObjectURL(url);
        },
        error: (err) => {
          // כאשר responseType הוא blob ויש שגיאה, נקרא את טקסט ה-blob
          if (err.error instanceof Blob) {
            err.error.text().then((text: string) => {
              try {
                const errorObj = JSON.parse(text);
                this.errorMsg = errorObj.message || text || 'Error downloading winners report';
              } catch {
                this.errorMsg = text || 'Error downloading winners report';
              }
            });
          } else if (typeof err.error === 'string') {
            this.errorMsg = err.error;
          } else {
            this.errorMsg = 'Error downloading winners report';
          }
        }
      });
  }
  // raffleAll() {
  //   this.giftSrv.raffleAllGifts().subscribe({
  //     next: (report: RaffleReportDTO) => {
  //       console.log('Raffle Report:', report);
  //       if (report && report.results && report.results.length > 0) {
  //         let message = `בוצעה הגרלה ל־${report.results.length} מתנות\nסך הכנסות: ₪${report.totalIncome}\n\nזוכים:\n`;
  //         report.results.forEach(result => {
  //           message += `${result.giftName}: ${result.winnerUserName}\n`;
  //         });
  //         alert(message);
  //         this.raffleResult = report.results;
  //         this.saveRaffleResultsToStorage();
  //       } else {
  //         alert('אין מתנות זמינות להגרלה (כבר הוגרלה או אין קניות)');
  //       }
  //       this.loadGifts();
  //     },
  //     error: (err) => {
  //       console.error('Raffle Error:', err);
  //       this.errorMsg = err.error || 'שגיאה בביצוע ההגרלה לכל המתנות';
  //       alert(this.errorMsg);
  //     }
  //   });
  // }
  
  getWinnerName(giftId: number): string | null {
    const found = this.raffleResult.find(r => r.giftId === giftId);
    return found ? found.winnerUserName : null;
  }

  // הצגת רשימת רוכשים למתנה - ניווט לעמוד ייעודי
  viewPurchasers(gift: GiftModel) {
    this.router.navigate(['/gifts', gift.id, 'purchasers']);
  }

  // הצגת סך הכנסות המכירות
  showTotalIncome() {
    this.giftSrv.getTotalIncome().subscribe({
      next: (response) => {
        alert(`💰 Total sales income: ${response.totalIncome.toFixed(2)}$`);
      },
      error: (err) => {
        this.errorMsg = 'Error loading total income';
      }
    });
  }
 
}
