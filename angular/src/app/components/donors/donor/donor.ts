import { Component, EventEmitter, inject, Input, Output, SimpleChanges } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { DonorService } from '../../../services/donor-service';
import { DonorModel } from '../../../models/donor-model';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-donor',
  standalone: true,
  imports: [ReactiveFormsModule, CommonModule],
  templateUrl: './donor.html',
  styleUrl: './donor.scss',
})
export class Donor {

  donorSrv: DonorService = inject(DonorService);

  @Input()
  selectedId: number = -1;

  @Output()
  selectedIdChange: EventEmitter<number> = new EventEmitter<number>();

  frmDonor: FormGroup = new FormGroup({
    id: new FormControl(0),
    name: new FormControl('', [Validators.required]),
    email: new FormControl('', [Validators.required, Validators.email]),
    phone: new FormControl('', [Validators.required]),
  });

  serverError: string | null = null;

  saveDonor() {
    this.serverError = null;
    if (this.frmDonor.invalid) {
      this.serverError = 'Please fill in all required fields';
      return;
    }

    const donorDTO: DonorModel = this.frmDonor.value;

    const obs = this.selectedId > 0
      ? this.donorSrv.updateDonor(donorDTO)
      : this.donorSrv.addDonor(donorDTO);

    obs.subscribe({
      next: () => this.selectedIdChange.emit(-1),
      error: (err) => {
        // Extract the error message from the response structure
        try {
          // Try to parse if it's a JSON string (because responseType is 'text')
          const errorObj = typeof err.error === 'string' ? JSON.parse(err.error) : err.error;
          
          if (errorObj?.errors && Array.isArray(errorObj.errors) && errorObj.errors.length > 0) {
            this.serverError = errorObj.errors[0].message;
          } else if (errorObj?.message) {
            this.serverError = errorObj.message;
          } else if (typeof err.error === 'string') {
            this.serverError = err.error;
          } else {
            this.serverError = 'שגיאה בשמירת תורם';
          }
        } catch {
          this.serverError = err.error || err.message || 'שגיאה בשמירת תורם';
        }
      }
    });
  }

  // מעדכן טופס כשיד תורם משתנה
  ngOnChanges(c: SimpleChanges){
    if(c['selectedId']){
      if(this.selectedId > 0){
        // עריכת תורם קיים
        this.donorSrv.getDonorById(this.selectedId).subscribe((donor) => {
          this.frmDonor.patchValue(donor);
        });
      } else if(this.selectedId === 0){
        // תורם חדש
        this.frmDonor.reset({ id: 0, name: '', email: '', phone: '' });
      }
    }
  }

  // ביטול עריכה
  cancel() {
    this.selectedIdChange.emit(-1);
  }
}
